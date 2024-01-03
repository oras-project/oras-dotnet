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

using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Remote;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Web.HttpUtility;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// Repository is an HTTP client to a remote repository
/// </summary>
public class Repository : IRepository, IRepositoryOption
{
    /// <summary>
    /// HttpClient is the underlying HTTP client used to access the remote registry.
    /// </summary>
    public HttpClient HttpClient { get; set; }

    /// <summary>
    /// ReferenceObj references the remote repository.
    /// </summary>
    public Reference RemoteReference { get; set; }

    /// <summary>
    /// PlainHTTP signals the transport to access the remote repository via HTTP
    /// instead of HTTPS.
    /// </summary>
    public bool PlainHTTP { get; set; }


    /// <summary>
    /// ManifestMediaTypes is used in `Accept` header for resolving manifests
    /// from references. It is also used in identifying manifests and blobs from
    /// descriptors. If an empty list is present, default manifest media types
    /// are used.
    /// </summary>
    public string[] ManifestMediaTypes { get; set; }

    /// <summary>
    /// TagListPageSize specifies the page size when invoking the tag list API.
    /// If zero, the page size is determined by the remote registry.
    /// Reference: https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    public int TagListPageSize { get; set; }

    /// <summary>
    /// Creates a client to the remote repository identified by a reference
    /// Example: localhost:5000/hello-world
    /// </summary>
    /// <param name="reference"></param>
    public Repository(string reference)
    {
        RemoteReference = Reference.Parse(reference);
        HttpClient = new HttpClient();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", new string[] { "oras-dotnet" });
    }

    /// <summary>
    /// Creates a client to the remote repository using a reference and a HttpClient
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="httpClient"></param>
    public Repository(string reference, HttpClient httpClient)
    {
        RemoteReference = Reference.Parse(reference);
        HttpClient = httpClient;
    }

    /// <summary>
    /// This constructor customizes the HttpClient and sets the properties
    /// using values from the parameter.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="option"></param>
    internal Repository(Reference reference, IRepositoryOption option)
    {
        HttpClient = option.HttpClient;
        RemoteReference = reference;
        ManifestMediaTypes = option.ManifestMediaTypes;
        PlainHTTP = option.PlainHTTP;
        TagListPageSize = option.TagListPageSize;
    }

    /// <summary>
    /// BlobStore detects the blob store for the given descriptor.
    /// </summary>
    /// <param name="desc"></param>
    /// <returns></returns>
    private IBlobStore BlobStore(Descriptor desc)
    {
        if (ManifestUtility.IsManifest(ManifestMediaTypes, desc))
        {
            return Manifests;
        }

        return Blobs;
    }



    /// <summary>
    /// FetchAsync fetches the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return await BlobStore(target).FetchAsync(target, cancellationToken);
    }

    /// <summary>
    /// ExistsAsync returns true if the described content exists.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return await BlobStore(target).ExistsAsync(target, cancellationToken);
    }

    /// <summary>
    /// PushAsync pushes the content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
    {
        await BlobStore(expected).PushAsync(expected, content, cancellationToken);
    }

    /// <summary>
    /// ResolveAsync resolves a reference to a manifest descriptor
    /// See all ManifestMediaTypes
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        return await Manifests.ResolveAsync(reference, cancellationToken);
    }

    /// <summary>
    /// TagAsync tags a manifest descriptor with a reference string.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        await Manifests.TagAsync(descriptor, reference, cancellationToken);
    }

    /// <summary>
    /// FetchReference fetches the manifest identified by the reference.
    /// The reference can be a tag or digest.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        return await Manifests.FetchAsync(reference, cancellationToken);
    }

    /// <summary>
    /// PushReference pushes the manifest with a reference tag.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor descriptor, Stream content, string reference,
        CancellationToken cancellationToken = default)
    {
        await Manifests.PushAsync(descriptor, content, reference, cancellationToken);
    }

    /// <summary>
    /// DeleteAsync removes the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        await BlobStore(target).DeleteAsync(target, cancellationToken);
    }

    /// <summary>
    /// TagsAsync returns a list of tags in a repository
    /// </summary>
    /// <param name="repo"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> TagsAsync(ITagListable repo, CancellationToken cancellationToken)
    {
        var tags = new List<string>();
        await foreach (var tag in repo.ListTagsAsync().WithCancellation(cancellationToken))
        {
            tags.Add(tag);
        }
        return tags;
    }

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
    /// <param name="fn"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> ListTagsAsync(string? last = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = URLUtiliity.BuildRepositoryTagListURL(PlainHTTP, RemoteReference);
        var done = false;
        while (!done)
        {
            IEnumerable<string> tags = Array.Empty<string>();
            try
            {
                url = await TagsPageAsync(last, values => tags = values, url, cancellationToken);
                last = "";
            }
            catch (LinkUtility.NoLinkHeaderException)
            {
                done = true;
            }
            foreach (var tag in tags)
            {
                yield return tag;
            }
        }
    }

    /// <summary>
    /// TagsPageAsync returns a single page of tag list with the next link.
    /// </summary>
    /// <param name="last"></param>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<string> TagsPageAsync(string? last, Action<string[]> fn, string url, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(url);
        var query = ParseQueryString(uriBuilder.Query);
        if (TagListPageSize > 0 || !string.IsNullOrEmpty(last))
        {
            if (TagListPageSize > 0)
            {
                query["n"] = TagListPageSize.ToString();
            }
            if (!string.IsNullOrEmpty(last))
            {
                query["last"] = last;
            }
        }

        uriBuilder.Query = query.ToString();
        using var resp = await HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken);
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            throw await ErrorUtility.ParseErrorResponse(resp);

        }
        var data = await resp.Content.ReadAsStringAsync();
        var tagList = JsonSerializer.Deserialize<ResponseTypes.TagList>(data);
        fn(tagList.Tags);
        return LinkUtility.ParseLink(resp);
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
    /// <exception cref="Exception"></exception>
    internal async Task DeleteAsync(Descriptor target, bool isManifest, CancellationToken cancellationToken)
    {
        var remoteReference = RemoteReference;
        remoteReference.ContentReference = target.Digest;
        string url;
        if (isManifest)
        {
            url = URLUtiliity.BuildRepositoryManifestURL(PlainHTTP, remoteReference);
        }
        else
        {
            url = URLUtiliity.BuildRepositoryBlobURL(PlainHTTP, remoteReference);
        }

        using var resp = await HttpClient.DeleteAsync(url, cancellationToken);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.Accepted:
                VerifyContentDigest(resp, target.Digest);
                break;
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"digest {target.Digest} not found");
            default:
                throw await ErrorUtility.ParseErrorResponse(resp);
        }
    }


    /// <summary>
    /// VerifyContentDigest verifies "Docker-Content-Digest" header if present.
    /// OCI distribution-spec states the Docker-Content-Digest header is optional.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#legacy-docker-support-http-headers
    /// </summary>
    /// <param name="resp"></param>
    /// <param name="expected"></param>
    /// <exception cref="NotImplementedException"></exception>
    internal static void VerifyContentDigest(HttpResponseMessage resp, string expected)
    {
        if (!resp.Content.Headers.TryGetValues("Docker-Content-Digest", out var digestValues)) return;
        var digestStr = digestValues.FirstOrDefault();
        if (string.IsNullOrEmpty(digestStr))
        {
            return;
        }

        string contentDigest;
        try
        {
            contentDigest = Digest.Validate(digestStr);
        }
        catch (Exception)
        {
            throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response header: `Docker-Content-Digest: {digestStr}`");
        }
        if (contentDigest != expected)
        {
            throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response; digest mismatch in Docker-Content-Digest: received {contentDigest} when expecting {digestStr}");
        }
    }


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
    /// <returns></returns>
    public Reference ParseReference(string reference)
    {
        Reference remoteReference;
        var hasError = false;
        try
        {
            remoteReference = Reference.Parse(reference);
        }
        catch (Exception)
        {
            hasError = true;
            //reference is not a FQDN
            var index = reference.IndexOf("@");
            if (index != -1)
            {
                // `@` implies *digest*, so drop the *tag* (irrespective of what it is).
                reference = reference[(index + 1)..];
            }
            remoteReference = new Reference(RemoteReference.Registry, RemoteReference.Repository, reference);
            if (index != -1)
            {
                _ = remoteReference.Digest;
            }
        }

        if (!hasError)
        {
            if (remoteReference.Registry != RemoteReference.Registry ||
                remoteReference.Repository != RemoteReference.Repository)
            {
                throw new InvalidReferenceException(
                    $"mismatch between received {JsonSerializer.Serialize(remoteReference)} and expected {JsonSerializer.Serialize(RemoteReference)}");
            }
        }
        if (string.IsNullOrEmpty(remoteReference.ContentReference))
        {
            throw new InvalidReferenceException();
        }
        return remoteReference;

    }


    /// <summary>
    /// GenerateBlobDescriptor returns a descriptor generated from the response.
    /// </summary>
    /// <param name="resp"></param>
    /// <param name="refDigest"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Descriptor GenerateBlobDescriptor(HttpResponseMessage resp, string refDigest)
    {
        var mediaType = resp.Content.Headers.ContentType.MediaType;
        if (string.IsNullOrEmpty(mediaType))
        {
            mediaType = "application/octet-stream";
        }
        var size = resp.Content.Headers.ContentLength.Value;
        if (size == -1)
        {
            throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: unknown response Content-Length");
        }

        VerifyContentDigest(resp, refDigest);

        return new Descriptor
        {
            MediaType = mediaType,
            Digest = refDigest,
            Size = size
        };
    }
}
