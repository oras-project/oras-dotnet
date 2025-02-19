// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// Repository is an HTTP client to a remote repository
/// </summary>
public class Repository : IRepository
{
    /// <summary>
    /// Blobs provides access to the blob CAS only, which contains
    /// layers, and other generic blobs.
    /// </summary>
    public IBlobStore Blobs => new BlobStore(this);

    /// <summary>
    /// Manifests provides access to the manifest CAS only.
    /// </summary>
    /// <returns></returns>
    public IManifestStore Manifests => new ManifestStore(this);

    public RepositoryOptions Options => _opts;
    
    private int _referrersState = (int) Referrers.ReferrersState.Unknown;
    
    /// <summary>
    ///  _filterTypeArtifactType is the "artifactType" filter applied on the list of referrers.
    ///
    /// References:
    ///   - Latest spec: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    private const string _filterTypeArtifactType = "artifactType";

    /// <summary>
    /// ReferrersState indicates the Referrers API state of the remote repository.
    /// ReferrersState can be set only once, otherwise it throws ReferrersStateAlreadySetException.
    /// </summary>
    internal Referrers.ReferrersState ReferrersState
    {
        get => (Referrers.ReferrersState) _referrersState;
        set
        {
            var originalReferrersState = (Referrers.ReferrersState) Interlocked.CompareExchange(ref _referrersState, (int)value, (int)Referrers.ReferrersState.Unknown);
            if (originalReferrersState != Referrers.ReferrersState.Unknown && _referrersState != (int)value)
            {
                throw new ReferrersStateAlreadySetException($"current referrers state: {ReferrersState}, latest referrers state: {value}");
            }
        }
    }

    /// <summary>
    /// ReferrerListPageSize specifies the page size when invoking the Referrers API.
    /// If zero, the page size is determined by the remote registry.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    public int ReferrerListPageSize; 
    
    /// <summary>
    /// _headerOciFiltersApplied is the "OCI-Filters-Applied" header.
    /// If present on the response, it contains a comma-separated list of the applied filters.
    /// Reference:
    ///   - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    private const string _headerOciFiltersApplied = "OCI-Filters-Applied";
    
    internal static readonly string[] DefaultManifestMediaTypes =
    [
        Docker.MediaType.Manifest,
        Docker.MediaType.ManifestList,
        MediaType.ImageIndex,
        MediaType.ImageManifest
    ];

    private RepositoryOptions _opts;

    /// <summary>
    /// Creates a client to the remote repository identified by a reference
    /// Example: localhost:5000/hello-world
    /// </summary>
    /// <param name="reference"></param>
    public Repository(string reference) : this(reference, new HttpClient().AddUserAgent()) { }

    /// <summary>
    /// Creates a client to the remote repository using a reference and a HttpClient
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="httpClient"></param>
    public Repository(string reference, HttpClient httpClient) : this(new RepositoryOptions()
    {
        Reference = Reference.Parse(reference),
        HttpClient = httpClient,
    })
    { }

    public Repository(RepositoryOptions options)
    {
        if (string.IsNullOrEmpty(options.Reference.Repository))
        {
            throw new InvalidReferenceException("Missing repository");
        }
        _opts = options;
    }

    /// <summary>
    /// FetchAsync fetches the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await BlobStore(target).FetchAsync(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// ExistsAsync returns true if the described content exists.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await BlobStore(target).ExistsAsync(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// PushAsync pushes the content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        => await BlobStore(expected).PushAsync(expected, content, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// ResolveAsync resolves a reference to a manifest descriptor
    /// See all ManifestMediaTypes
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        => await Manifests.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// TagAsync tags a manifest descriptor with a reference string.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        => await Manifests.TagAsync(descriptor, reference, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// FetchReference fetches the manifest identified by the reference.
    /// The reference can be a tag or digest.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
        => await Manifests.FetchAsync(reference, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// PushReference pushes the manifest with a reference tag.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor descriptor, Stream content, string reference, CancellationToken cancellationToken = default)
        => await Manifests.PushAsync(descriptor, content, reference, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// DeleteAsync removes the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await BlobStore(target).DeleteAsync(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// TagsAsync lists the tags available in the repository.
    /// See also `TagListPageSize`.
    /// If `last` is NOT empty, the entries in the response start after the
    /// tag specified by `last`. Otherwise, the response starts from the top
    /// of the Tags list.
    /// References:
    /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#content-discovery
    /// - https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    /// <param name="last"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> ListTagsAsync(string? last = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = new UriFactory(_opts).BuildRepositoryTagList();
        do
        {
            (var tags, url) = await FetchTagsPageAsync(last, url!, cancellationToken).ConfigureAwait(false);
            last = null;
            foreach (var tag in tags)
            {
                yield return tag;
            }
        } while (url != null);
    }

    /// <summary>
    /// Returns a single page of tag list with the next link.
    /// </summary>
    /// <param name="last"></param>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<(string[], Uri?)> FetchTagsPageAsync(string? last, Uri url, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(url);
        if (_opts.TagListPageSize > 0 || !string.IsNullOrEmpty(last))
        {
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (_opts.TagListPageSize > 0)
            {
                query["n"] = _opts.TagListPageSize.ToString();
            }
            if (!string.IsNullOrEmpty(last))
            {
                query["last"] = last;
            }
            uriBuilder.Query = query.ToString();
        }

        using var response = await _opts.HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var tagList = JsonSerializer.Deserialize<TagList>(data);
        return (tagList.Tags, response.ParseLink());
    }

    internal struct TagList
    {
        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }
    }

    /// <summary>
    /// DeleteAsync removes the content identified by the descriptor in the
    /// entity blobs or manifests.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="isManifest"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    internal async Task DeleteAsync(Descriptor target, bool isManifest, CancellationToken cancellationToken)
    {
        var remoteReference = ParseReferenceFromDigest(target.Digest);
        var uriFactory = new UriFactory(remoteReference, _opts.PlainHttp);
        var url = isManifest ? uriFactory.BuildRepositoryManifest() : uriFactory.BuildRepositoryBlob();

        using var resp = await _opts.HttpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        switch (resp.StatusCode)
        {
            case HttpStatusCode.Accepted:
                resp.VerifyContentDigest(target.Digest);
                break;
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"Digest {target.Digest} not found");
            default:
                throw await resp.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// ParseReference resolves a tag or a digest reference to a fully qualified
    /// reference from a base reference Reference.
    /// Tag, digest, or fully qualified references are accepted as input.
    /// If reference is a fully qualified reference, then ParseReference parses it
    /// and returns the parsed reference. If the parsed reference does not share
    /// the same base reference with the Repository, ParseReference throws an
    /// error, InvalidReferenceException.
    /// </summary>
    /// <param name="reference"></param>
    internal Reference ParseReference(string reference)
    {
        if (Reference.TryParse(reference, out var remoteReference))
        {
            if (remoteReference.Registry != _opts.Reference.Registry || remoteReference.Repository != _opts.Reference.Repository)
            {
                throw new InvalidReferenceException($"Mismatch between received {JsonSerializer.Serialize(remoteReference)} and expected {JsonSerializer.Serialize(_opts.Reference)}");
            }
        }
        else
        {
            var index = reference.IndexOf("@");
            if (index != -1)
            {
                // `@` implies *digest*, so drop the *tag* (irrespective of what it is).
                reference = reference[(index + 1)..];
            }
            remoteReference = new Reference(_opts.Reference.Registry, _opts.Reference.Repository, reference);
            if (index != -1)
            {
                _ = remoteReference.Digest;
            }
        }
        if (string.IsNullOrEmpty(remoteReference.ContentReference))
        {
            throw new InvalidReferenceException("Empty content reference");
        }
        return remoteReference;
    }

    internal Reference ParseReferenceFromDigest(string digest)
    {
        var reference = new Reference(_opts.Reference.Registry, _opts.Reference.Repository, digest);
        _ = reference.Digest;
        return reference;
    }

    internal Reference ParseReferenceFromContentReference(string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            throw new InvalidReferenceException("Empty content reference");
        }
        return new Reference(_opts.Reference.Registry, _opts.Reference.Repository, reference);
    }

    /// <summary>
    /// Returns the accept header for manifest media types.
    /// </summary>
    internal string ManifestAcceptHeader() => string.Join(',', _opts.ManifestMediaTypes ?? DefaultManifestMediaTypes);

    /// <summary>
    /// Determines if the given descriptor is a manifest.
    /// </summary>
    /// <param name="desc"></param>
    private bool IsManifest(Descriptor desc) => (_opts.ManifestMediaTypes ?? DefaultManifestMediaTypes).Any(mediaType => mediaType == desc.MediaType);

    /// <summary>
    /// Detects the blob store for the given descriptor.
    /// </summary>
    /// <param name="desc"></param>
    /// <returns></returns>
    private IBlobStore BlobStore(Descriptor desc) => IsManifest(desc) ? Manifests : Blobs;

    /// <summary>
    /// Mount makes the blob with the given digest in fromRepo
    /// available in the repository signified by the receiver.
    ///
    /// This avoids the need to pull content down from fromRepo only to push it to r.
    ///
    /// If the registry does not implement mounting, getContent will be used to get the
    /// content to push. If getContent is null, the content will be pulled from the source
    /// repository.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="fromRepository"></param>
    /// <param name="getContent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task MountAsync(Descriptor descriptor, string fromRepository, Func<CancellationToken, Task<Stream>>? getContent = null, CancellationToken cancellationToken = default) 
        => await ((IMounter)Blobs).MountAsync(descriptor, fromRepository, getContent, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// ReferrersAsync retrieves referrers for the given descriptor and artifact type if specified
    /// and executes a provided callback function with the list of descriptors.
    /// If referrers API is not supported, the function falls back to a tag schema for retrieving referrers.
    /// If the referrers are supported via an API, the state is updated accordingly.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="artifactType"></param>
    /// <param name="fn"></param>
    /// <param name="cancellationToken"></param>
    public async Task ReferrersAsync(Descriptor descriptor, string? artifactType, Action<IList<Descriptor>> fn, CancellationToken cancellationToken = default)
    {
        if (ReferrersState == Referrers.ReferrersState.NotSupported)
        {
            // fall back to tag schema to retrieve referrers
            await ReferrersByTagSchema(descriptor, artifactType, fn, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {   
            await ReferrersByApi(descriptor, artifactType, fn, cancellationToken).ConfigureAwait(false);
            if (ReferrersState == Referrers.ReferrersState.Supported)
            {
                return;
            }
        }
        catch (NotSupportedException)
        {
            // fall back to tag schema to retrieve referrers if NotSupportedException was raised
            SetReferrersState(false);
            await ReferrersByTagSchema(descriptor, artifactType, fn, cancellationToken).ConfigureAwait(false);
            return;
        }
        // Set it to supported when no exception was thrown
        SetReferrersState(true);
    }
    
    /// <summary>
    /// ReferrersByApi retrieves referrers for the given descriptor and artifact type if specified
    /// and executes a provided callback function with the list of descriptors
    /// only when referrers API is supported.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="artifactType"></param>
    /// <param name="fn"></param>
    /// <param name="cancellationToken"></param>
    internal async Task ReferrersByApi(Descriptor descriptor, string? artifactType,
        Action<IList<Descriptor>> fn,
        CancellationToken cancellationToken = default)
    {
        var reference = Options.Reference.Clone();
        reference.ContentReference = descriptor.Digest;
        var url = new UriFactory(reference).BuildReferrersUrl(artifactType);
        while (url != null)
        {
            // Call ReferrersPageByApi to fetch a page of referrers, passing the URL and the callback function.
            // The method will return the next URL (if available) for pagination.
            url = await ReferrersPageByApi(descriptor, artifactType, url, fn, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// ReferrersPageByApi retrieves a page of referrers from the specified URL, processes the response, and executes a callback with the referrers.
    /// If the response content is valid and matches expected conditions (e.g., media type, digest),
    /// the referrers are extracted and passed to the callback function.
    /// The function handles pagination by returning the next URL for further referrers, if available.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="artifactType"></param>
    /// <param name="url"></param>
    /// <param name="fn"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ResponseException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="InvalidResponseException"></exception>
    private async Task<Uri?> ReferrersPageByApi(Descriptor descriptor, string? artifactType, Uri url, Action<IList<Descriptor>> fn,
        CancellationToken cancellationToken = default)
    {
        // If ReferrerListPageSize is greater than 0, modify the URL to include the page size query parameter
        if (ReferrerListPageSize > 0)
        {
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("n", ReferrerListPageSize.ToString());
            uriBuilder.Query = query.ToString();
            url = uriBuilder.Uri;
        }
        using var response = await _opts.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                // If the status code is OK, continue processing the response
                break;
            case HttpStatusCode.NotFound:
                // If the status code is NotFound, handle as an error, possibly a non-existent repository
                var err = (ResponseException) await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
                if (err.Errors?.First().Code == ResponseException.ErrorCodeNameUnknown)
                {
                    // Repository is not found, Referrers API status is unknown
                    throw err;
                }
                
                SetReferrersState(false);
                throw new NotSupportedException("failed to query referrers API");
            default:
                // For any other status code, parse and throw the error response
                throw (ResponseException) await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != MediaType.ImageIndex)
        {
            throw new NotSupportedException($"unknown content returned {mediaType}, expecting image index");
        }
        response.VerifyContentDigest(descriptor.Digest);
        using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var referrersIndex = JsonSerializer.Deserialize<Index>(content);
        if (referrersIndex == null)
        {
            throw new InvalidResponseException($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: failed to decode response");
        }
        var referrers = referrersIndex.Manifests;
        
        // If artifactType is specified, apply any filters based on the artifact type
        if (!string.IsNullOrEmpty(artifactType))
        {
            if (!response.Headers.TryGetValues(_headerOciFiltersApplied, out var values) || !Referrers.IsReferrersFilterApplied(values.FirstOrDefault(), _filterTypeArtifactType))
            {
                // Filter the referrers based on the artifact type if necessary
                referrers = Referrers.FilterReferrers(referrers, artifactType);
            }
        }

        if (referrers.Count > 0)
        {
            fn(referrers);
        }

        return response.ParseLink();
    }
    
    /// <summary>
    /// ReferrersByTagSchema retrieves referrers based on referrers tag schema, filters out referrers based on specified artifact type
    /// and invoke callback function fn when referrers are returned when referrers API is not supported.
    /// 
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#backwards-compatibility
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="artifactType"></param>
    /// <param name="fn"></param>
    /// <param name="cancellationToken"></param>
    internal async Task ReferrersByTagSchema(Descriptor descriptor, string? artifactType, Action<IList<Descriptor>> fn,
        CancellationToken cancellationToken = default)
    {
        var referrersTag = Referrers.BuildReferrersTag(descriptor);
        var (_, referrers) = await PullReferrersIndexList(referrersTag, cancellationToken).ConfigureAwait(false);
        var filteredReferrers = Referrers.FilterReferrers(referrers, artifactType);
        if (filteredReferrers.Count > 0)
        {
            fn(filteredReferrers);
        }
    }

    /// <summary>
    /// PullReferrersIndexList retrieves the referrers index list associated with the given referrers tag.
    /// It fetches the index manifest from the repository, deserializes it into an `Index` object, 
    /// and returns the descriptor along with the list of manifests (referrers). If the referrers index is not found, 
    /// an empty descriptor and an empty list are returned.
    /// </summary>
    /// <param name="referrersTag"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<(Descriptor?, IList<Descriptor>)> PullReferrersIndexList(String referrersTag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await FetchAsync(referrersTag, cancellationToken).ConfigureAwait(false);
            using (var content = result.Item2)
            {
                var index = JsonSerializer.Deserialize<Index>(content);
                if (index == null)
                {
                    throw new JsonException($"null index manifests list for referrersTag {referrersTag}");
                }

                return (result.Item1, index.Manifests);
            }
        }
        catch (NotFoundException)
        {
            return (null, ImmutableArray<Descriptor>.Empty);
        }
    }

    /// <summary>
    /// SetReferrersState indicates the Referrers API state of the remote repository. true: supported; false: not supported.
    /// SetReferrersState is valid only when it is called for the first time.
    /// SetReferrersState returns ReferrersStateAlreadySetException if the Referrers API state has been already set.
    ///   - When the state is set to true, the relevant functions will always
    ///     request the Referrers API. Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#listing-referrers
    ///   - When the state is set to false, the relevant functions will always
    ///     request the Referrers Tag. Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#referrers-tag-schema
    ///   - When the state is not set, the relevant functions will automatically
    ///     determine which API to use.
    /// </summary>
    /// <param name="isSupported"></param>
    public void SetReferrersState(bool isSupported)
    {
        ReferrersState = isSupported ? Referrers.ReferrersState.Supported : Referrers.ReferrersState.NotSupported;
    }
}
