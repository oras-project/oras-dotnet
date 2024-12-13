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
}
