﻿using Oras.Exceptions;
using Oras.Interfaces.Registry;
using Oras.Models;
using Oras.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Web.HttpUtility;
namespace Oras.Remote
{
    public class RepositoryOption : IRepositoryOption
    {
        public HttpClient HttpClient { get; set; }
        public RemoteReference RemoteReference { get; set; }
        public bool PlainHTTP { get; set; }
        public string[] ManifestMediaTypes { get; set; }
        public int TagListPageSize { get; set; }
        public long MaxMetadataBytes { get; set; }
    }
    /// <summary>
    /// Repository is an HTTP client to a remote repository
    /// </summary>
    public class Repository : IRepository, IRepositoryOption
    {
        /// <summary>
        /// bytes are allowed in the server's response to the metadata APIs.
        /// defaultMaxMetadataBytes specifies the default limit on how many response
        /// See also: Repository.MaxMetadataBytes
        /// </summary>
        public long defaultMaxMetaBytes = 4 * 1024 * 1024; //4 Mib

        /// <summary>
        /// HttpClient is the underlying HTTP client used to access the remote registry.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// ReferenceObj references the remote repository.
        /// </summary>
        public RemoteReference RemoteReference { get; set; }

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
        /// MaxMetadataBytes specifies a limit on how many response bytes are allowed
        /// in the server's response to the metadata APIs, such as catalog list, tag
        /// list, and referrers list.
        /// If less than or equal to zero, a default (currently 4MiB) is used.
        /// </summary>
        public long MaxMetadataBytes { get; set; }

        /// <summary>
        /// dockerContentDigestHeader - The Docker-Content-Digest header, if present
        /// on the response, returns the canonical digest of the uploaded blob.
        /// See https://docs.docker.com/registry/spec/api/#digest-header
        /// See https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#content-digests
        /// </summary>
        public const string DockerContentDigestHeader = "Docker-Content-Digest";

        /// <summary>
        /// Creates a client to the remote repository identified by a reference
        /// Example: localhost:5000/hello-world
        /// </summary>
        /// <param name="reference"></param>
        public Repository(string reference)
        {
            var refObj = new RemoteReference().ParseReference(reference);
            RemoteReference = refObj;
        }

        /// <summary>
        /// This constructor customizes the Properties using the values
        /// from the RepositoryOptions.
        /// RepositoryOptions contains unexported state that must not be copied
        /// to multiple Repositories. To handle this we explicitly copy only the
        /// fields that we want to reproduce.
        /// </summary>
        /// <param name="ref"></param>
        /// <param name="option"></param>
        public Repository(RemoteReference @ref, IRepositoryOption option)
        {
            @ref.ValidateRepository();
            HttpClient = option.HttpClient;
            RemoteReference = @ref;
            PlainHTTP = option.PlainHTTP;
            ManifestMediaTypes = option.ManifestMediaTypes;
            TagListPageSize = option.TagListPageSize;
            MaxMetadataBytes = option.MaxMetadataBytes;

        }

        /// <summary>
        /// Client returns an HTTP client used to access the remote repository.
        /// A default HTTP client is return if the client is not configured.
        /// </summary>
        /// <returns></returns>
        private HttpClient Client()
        {
            if (HttpClient is null)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", new string[] { "oras-dotnet" });
                return client;
            }

            return HttpClient;
        }


        /// <summary>
        /// blobStore detects the blob store for the given descriptor.
        /// </summary>
        /// <param name="desc"></param>
        /// <returns></returns>
        private IBlobStore blobStore(Descriptor desc)
        {
            if (ManifestUtil.IsManifest(ManifestMediaTypes, desc))
            {
                return Manifests();
            }

            return Blobs();
        }



        /// <summary>
        /// FetchAsync fetches the content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await blobStore(target).FetchAsync(target, cancellationToken);
        }

        /// <summary>
        /// ExistsAsync returns true if the described content exists.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await blobStore(target).ExistsAsync(target, cancellationToken);
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
            await blobStore(expected).PushAsync(expected, content, cancellationToken);
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
            return await Manifests().ResolveAsync(reference, cancellationToken);
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
            await Manifests().TagAsync(descriptor, reference, cancellationToken);
        }

        /// <summary>
        /// FetchReference fetches the manifest identified by the reference.
        /// The reference can be a tag or digest.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(Descriptor Descriptor, Stream Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await Manifests().FetchReferenceAsync(reference, cancellationToken);
        }

        /// <summary>
        /// PushReference pushes the manifest with a reference tag.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="content"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PushReferenceAsync(Descriptor descriptor, Stream content, string reference,
            CancellationToken cancellationToken = default)
        {
            await Manifests().PushReferenceAsync(descriptor, content, reference, cancellationToken);
        }

        /// <summary>
        /// DeleteAsync removes the content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            await blobStore(target).DeleteAsync(target, cancellationToken);
        }
        public async Task<List<string>> TagsAsync(ITagLister repo, CancellationToken cancellationToken)
        {
            var res = new List<string>();
            await repo.TagsAsync(
                string.Empty,
                 (tags) =>
                {
                    res.AddRange(tags);

                }, cancellationToken);
            return res;
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
        public async Task TagsAsync(string last, Action<string[]> fn, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = URLUtiliity.BuildRepositoryTagListURL(PlainHTTP, RemoteReference);
                while (true)
                {
                    url = await TagsPageAsync(last, fn, url, cancellationToken);
                    last = "";
                }
            }
            catch (Utils.NoLinkHeaderException)
            {
                return;
            }

        }

        /// <summary>
        /// TagsPageAsync returns a single page of tag list with the next link.
        /// </summary>
        /// <param name="last"></param>
        /// <param name="fn"></param>
        /// <param name="url"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> TagsPageAsync(string last, Action<string[]> fn, string url, CancellationToken cancellationToken)
        {
            if (PlainHTTP)
            {
                if (!url.Contains("http"))
                {
                    url = "http://" + url;
                }
            }
            else
            {
                if (!url.Contains("https"))
                {
                    url = "https://" + url;
                }
            }
            var uriBuilder = new UriBuilder(url);
            var query = ParseQueryString(uriBuilder.Query);
            if (TagListPageSize > 0 || last != "")
            {
                if (TagListPageSize > 0)
                {
                    query["n"] = TagListPageSize.ToString();


                }
                if (last != "")
                {
                    query["last"] = last;
                }
            }

            uriBuilder.Query = query.ToString();
            var resp = await HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken);
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw await ErrorUtil.ParseErrorResponse(resp);

            }
            var data = await resp.Content.ReadAsStringAsync();
            var tags = JsonSerializer.Deserialize<string[]>(data);
            fn(tags);
            return Utils.ParseLink(resp);
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
            var refObj = RemoteReference;
            refObj.Reference = target.Digest;
            Func<bool, RemoteReference, string> buildURL = URLUtiliity.BuildRepositoryBlobURL;
            if (isManifest)
            {
                buildURL = URLUtiliity.BuildRepositoryManifestURL;
            }

            var url = buildURL(PlainHTTP, refObj);
            var resp = await HttpClient.DeleteAsync(url, cancellationToken);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.Accepted:
                    VerifyContentDigest(resp, target.Digest);
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"digest {target.Digest} not found");
                default:
                    throw await ErrorUtil.ParseErrorResponse(resp);
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
        private void VerifyContentDigest(HttpResponseMessage resp, string expected)
        {
            resp.Content.Headers.TryGetValues(DockerContentDigestHeader, out var digestStr);
            if (digestStr == null || !digestStr.Any())
            {
                return;
            }

            string contentDigest;
            try
            {
                contentDigest = DigestUtility.Parse(digestStr.FirstOrDefault());
            }
            catch (Exception)
            {
                throw new Exception(
                   $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response header: {DockerContentDigestHeader}: {digestStr}"
                    );
            }

            if (contentDigest != expected)
            {
                throw new Exception(
$"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response; digest mismatch in {DockerContentDigestHeader}: received {contentDigest}, while expecting {expected}"
                    );
            }
            return;
        }


        /// <summary>
        /// Blobs provides access to the blob CAS only, which contains
        /// layers, and other generic blobs.
        /// </summary>
        /// <returns></returns>
        public IBlobStore Blobs()
        {
            return new BlobStore(this);
        }


        /// <summary>
        /// Manifests provides access to the manifest CAS only.
        /// </summary>
        /// <returns></returns>
        public IManifestStore Manifests()
        {
            return new ManifestStore(this);
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
        /// <returns></returns>
        public RemoteReference ParseReference(string reference)
        {
            try
            {
                var refObj = new RemoteReference().ParseReference(reference);
                if (refObj.Registry != RemoteReference.Registry || refObj.Repository != RemoteReference.Repository)
                {
                    throw new InvalidReferenceException(
                        $"mismatch between received {JsonSerializer.Serialize(refObj)} and expected {JsonSerializer.Serialize(RemoteReference)}");
                }

                if (string.IsNullOrEmpty(refObj.Reference))
                {
                    throw new InvalidReferenceException();
                }
                return refObj;
            }
            catch (Exception)
            {
                var refObj = new RemoteReference
                {
                    Registry = RemoteReference.Registry,
                    Repository = RemoteReference.Repository,
                    Reference = reference
                };
                //reference is not a FQDN
                if (reference.IndexOf("@") is var index && index != -1)
                {
                    // `@` implies *digest*, so drop the *tag* (irrespective of what it is).
                    refObj.Reference = reference[(index + 1)..];
                    refObj.ValidateReferenceAsDigest();
                }
                else
                {
                    refObj.ValidateReference();
                }
                return refObj;
            }

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
            var size = resp.Content.Headers.ContentLength!.Value;
            if (size == -1)
            {
                throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: unknown response Content-Length");
            }

            RemoteReference.VerifyContentDigest(resp, refDigest);

            return new Descriptor
            {
                MediaType = mediaType,
                Digest = refDigest,
                Size = size
            };
        }

    }

    public class ManifestStore : IManifestStore
    {
        public Repository Repository { get; set; }
        public ManifestStore(Repository repository)
        {
            Repository = repository;

        }

        /// <summary>
        /// FetchASync fetches the content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.RemoteReference;
            refObj.Reference = target.Digest;
            var url = URLUtiliity.BuildRepositoryManifestURL(Repository.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", target.MediaType);
            var resp = await Repository.HttpClient.SendAsync(req, cancellationToken);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"digest {target.Digest} not found");
                default:
                    throw await ErrorUtil.ParseErrorResponse(resp);
            }
            var mediaType = resp.Content.Headers?.ContentType.MediaType;
            if (mediaType != target.MediaType)
            {
                throw new Exception(
                    $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch response Content-Type {mediaType}: expect {target.MediaType}");
            }
            if (resp.Content.Headers!.ContentLength is var size && size != -1 && size != target.Size)
            {
                throw new Exception(
                    $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch Content-Length");
            }
            RemoteReference.VerifyContentDigest(resp, target.Digest);
            return await resp.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// ExistsAsync returns true if the described content exists.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            try
            {
                await ResolveAsync(target.Digest, cancellationToken);
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }

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
            await PushAsync(expected, content, expected.Digest, cancellationToken);
        }


        /// <summary>
        /// PushAsync pushes the manifest content, matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="stream"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        private async Task PushAsync(Descriptor expected, Stream stream, string reference, CancellationToken cancellationToken)
        {
            var refObj = Repository.RemoteReference;
            refObj.Reference = reference;
            var url = URLUtiliity.BuildRepositoryManifestURL(Repository.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Content = new StreamContent(stream);
            req.Content.Headers.ContentLength = expected.Size;
            req.Content.Headers.Add("Content-Type", expected.MediaType);
            var client = Repository.HttpClient;
            var resp = await client.SendAsync(req, cancellationToken);
            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw await ErrorUtil.ParseErrorResponse(resp);
            }
            RemoteReference.VerifyContentDigest(resp, expected.Digest);
        }

        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.ParseReference(reference);
            var url = URLUtiliity.BuildRepositoryManifestURL(Repository.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            req.Headers.Add("Accept", ManifestUtil.ManifestAcceptHeader(Repository.ManifestMediaTypes));
            var res = await Repository.HttpClient.SendAsync(req, cancellationToken);

            return res.StatusCode switch
            {
                HttpStatusCode.OK => GenerateDescriptor(res, refObj, req.Method),
                HttpStatusCode.NotFound => throw new NotFoundException($"reference {reference} not found"),
                _ => throw await ErrorUtil.ParseErrorResponse(res)
            };
        }

        /// <summary>
        /// GenerateDescriptor returns a descriptor generated from the response.
        /// </summary>
        /// <param name="res"></param>
        /// <param name="ref"></param>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Descriptor GenerateDescriptor(HttpResponseMessage res, RemoteReference @ref, HttpMethod httpMethod)
        {
            string mediaType;
            try
            {
                // 1. Validate Content-Type
                mediaType = res.Content.Headers.ContentType.MediaType;
                MediaTypeHeaderValue.Parse(mediaType);
            }
            catch (Exception e)
            {
                throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: invalid response `Content-Type` header; {e.Message}");
            }

            // 2. Validate Size
            if (!res.Content.Headers.ContentLength.HasValue)
            {
                throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: unknown response Content-Length");
            }

            // 3. Validate Client Reference
            string refDigest = string.Empty;
            try
            {
                refDigest = @ref.Digest();
            }
            catch (Exception)
            {
            }

            // 4. Validate Server Digest (if present)
            res.Content.Headers.TryGetValues("Docker-Content-Digest", out var serverHeaderDigest);
            var serverDigest = serverHeaderDigest.First();
            if (!string.IsNullOrEmpty(serverDigest))
            {
                try
                {
                    RemoteReference.VerifyContentDigest(res, serverDigest);
                }
                catch (Exception)
                {
                    throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: invalid response header value: `Docker-Content-Digest: {serverHeaderDigest}`");
                }
            }

            // 5. Now, look for specific error conditions;
            string contentDigest;

            if (string.IsNullOrEmpty(serverDigest))
            {
                if (httpMethod == HttpMethod.Head)
                {
                    if (string.IsNullOrEmpty(refDigest))
                    {
                        // HEAD without server `Docker-Content-Digest`
                        // immediate fail
                        throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: HTTP {httpMethod} request missing required header {serverHeaderDigest}");
                    }
                    // Otherwise, just trust the client-supplied digest
                    contentDigest = refDigest;
                }
                else
                {
                    // GET without server `Docker-Content-Digest header forces the
                    // expensive calculation
                    string calculatedDigest;
                    try
                    {
                        calculatedDigest = calculateDigestFromResponse(res, Repository.MaxMetadataBytes);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"failed to calculate digest on response body; {e.Message}");
                    }
                    contentDigest = calculatedDigest;
                }
            }
            else
            {
                contentDigest = serverDigest;
            }
            if (refDigest.Length > 0 && refDigest != contentDigest)
            {
                throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: invalid response; digest mismatch in {serverHeaderDigest}: received {contentDigest} when expecting {refDigest}");
            }

            // 6. Finally, if we made it this far, then all is good; return the descriptor
            return new Descriptor
            {
                MediaType = mediaType,
                Digest = contentDigest,
                Size = res.Content.Headers.ContentLength.Value
            };
        }

        /// <summary>
        /// CalculateDigestFromResponse calculates the actual digest of the response body
        /// taking care not to destroy it in the process
        /// </summary>
        /// <param name="res"></param>
        /// <param name="maxMetadataBytes"></param>
        private string calculateDigestFromResponse(HttpResponseMessage res, long maxMetadataBytes)
        {
            byte[] content = null;
            try
            {
                content = Utils.LimitReader(res.Content, maxMetadataBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: failed to read response body: {ex.Message}");
            }
            return DigestUtility.FromBytes(content);
        }

        /// <summary>
        /// DeleteAsync removes the manifest content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            await Repository.DeleteAsync(target, true, cancellationToken);
        }


        /// <summary>
        /// FetchReferenceAsync fetches the manifest identified by the reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(Descriptor Descriptor, Stream Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.ParseReference(reference);
            var url = URLUtiliity.BuildRepositoryManifestURL(Repository.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", ManifestUtil.ManifestAcceptHeader(Repository.ManifestMediaTypes));
            var resp = await Repository.HttpClient.SendAsync(req, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    Descriptor desc;
                    if (resp.Content.Headers.ContentLength == -1)
                    {
                        desc = await ResolveAsync(reference, cancellationToken);
                    }
                    else
                    {
                        desc = GenerateDescriptor(resp, refObj, HttpMethod.Get);
                    }
                    return (desc, await resp.Content.ReadAsStreamAsync());
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{req.Method} {req.RequestUri}: manifest unknown");
                default:
                    throw await ErrorUtil.ParseErrorResponse(resp);

            }
        }

        /// <summary>
        /// PushReferenceASync pushes the manifest with a reference tag.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="content"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PushReferenceAsync(Descriptor expected, Stream content, string reference,
            CancellationToken cancellationToken = default)
        {
            var refObj = Repository.ParseReference(reference);
            await PushAsync(expected, content, refObj.Reference, cancellationToken);
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
            var refObj = Repository.ParseReference(reference);
            var rc = await FetchAsync(descriptor, cancellationToken);
            await PushAsync(descriptor, rc, refObj.Reference, cancellationToken);
        }
    }

    internal class BlobStore : IBlobStore
    {

        public Repository Repository { get; set; }

        public BlobStore(Repository repository)
        {
            Repository = repository;

        }


        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.RemoteReference;
            DigestUtility.Parse(target.Digest);
            refObj.Reference = target.Digest;
            var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, refObj);
            var resp = await Repository.HttpClient.GetAsync(url, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    // server does not support seek as `Range` was ignored.
                    if (resp.Content.Headers.ContentLength is var size && size != -1 && size != target.Size)
                    {
                        throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch Content-Length");
                    }

                    return await resp.Content.ReadAsStreamAsync();
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{target.Digest}: not found");
                default:
                    throw await ErrorUtil.ParseErrorResponse(resp);
            }
        }

        /// <summary>
        /// ExistsAsync returns true if the described content exists.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            try
            {
                await ResolveAsync(target.Digest, cancellationToken);
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }

        }

        /// <summary>
        /// PushAsync pushes the content, matching the expected descriptor.
        /// Existing content is not checked by PushAsync() to minimize the number of out-going
        /// requests.
        /// Push is done by conventional 2-step monolithic upload instead of a single
        /// `POST` request for better overall performance. It also allows early fail on
        /// authentication errors.
        /// References:
        /// - https://docs.docker.com/registry/spec/api/#pushing-an-image
        /// - https://docs.docker.com/registry/spec/api/#initiate-blob-upload
        /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pushing-a-blob-monolithically
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        {
            var url = URLUtiliity.BuildRepositoryBlobUploadURL(Repository.PlainHTTP, Repository.RemoteReference);
            var resp = await Repository.HttpClient.PostAsync(url, null, cancellationToken);
            var reqHostname = resp.RequestMessage.RequestUri.Host;
            var reqPort = resp.RequestMessage.RequestUri.Port;
            if (resp.StatusCode != HttpStatusCode.Accepted)
            {
                throw await ErrorUtil.ParseErrorResponse(resp);
            }

            string location;
            // monolithic upload
            if (!resp.Headers.Location.IsAbsoluteUri)
            {
                location = resp.RequestMessage.RequestUri.Scheme + "://" + resp.RequestMessage.RequestUri.Authority + resp.Headers.Location;
            }
            else
            {
                location = resp.Headers.Location.ToString();
            }
            // work-around solution for https://github.com/oras-project/oras-go/issues/177
            // For some registries, if the port 443 is explicitly set to the hostname                                                                                                                                                        plicitly set to the hostname
            // like registry.wabbit-networks.io:443/myrepo, blob push will fail since
            // the hostname of the Location header in the response is set to
            // registry.wabbit-networks.io instead of registry.wabbit-networks.io:443.
            var uri = new Uri(location);
            var locationHostname = uri.Host;
            var locationPort = uri.Port;
            // if location port 443 is missing, add it back
            if (reqPort == 443 && locationHostname == reqHostname && locationPort != reqPort)
            {
                location = new Uri($"{locationHostname}:{reqPort}").ToString();
            }

            url = location;

            var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Content = new StreamContent(content);
            if (req.Content != null && req.Content.Headers.ContentLength is var size && size != expected.Size)
            {
                throw new Exception($"mismatch content length {size}: expect {expected.Size}");
            }
            req.Content.Headers.ContentLength = expected.Size;

            // the expected media type is ignored as in the API doc.
            req.Content.Headers.Add("Content-Type", "application/octet-stream");

            // add digest key to query string with expected digest value
            var query = ParseQueryString(new Uri(location).Query);
            query.Add("digest", expected.Digest);
            req.RequestUri = new Uri($"{req.RequestUri}?digest={expected.Digest}");

            //reuse credential from previous POST request
            resp.Headers.TryGetValues("Authorization", out var auth);
            if (auth != null)
            {
                req.Headers.Add("Authorization", auth.FirstOrDefault());
            }
            resp = await Repository.HttpClient.SendAsync(req, cancellationToken);
            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw await ErrorUtil.ParseErrorResponse(resp);
            }

            return;
        }

        /// <summary>
        /// ResolveAsync resolves a reference to a descriptor.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.ParseReference(reference);
            var refDigest = refObj.Digest();
            var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, refObj);
            var requestMessage = new HttpRequestMessage(HttpMethod.Head, url);
            var resp = await Repository.HttpClient.SendAsync(requestMessage, cancellationToken);
            return resp.StatusCode switch
            {
                HttpStatusCode.OK => Repository.GenerateBlobDescriptor(resp, refDigest),
                HttpStatusCode.NotFound => throw new NotFoundException($"{refObj.Reference}: not found"),
                _ => throw await ErrorUtil.ParseErrorResponse(resp)
            };
        }

        /// <summary>
        /// DeleteAsync deletes the content identified by the given descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            await Repository.DeleteAsync(target, false, cancellationToken);
        }

        /// <summary>
        /// FetchReferenceAsync fetches the blob identified by the reference.
        /// The reference must be a digest.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(Descriptor Descriptor, Stream Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repository.ParseReference(reference);
            var refDigest = refObj.Digest();
            var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, refObj);
            var resp = await Repository.HttpClient.GetAsync(url, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    // server does not support seek as `Range` was ignored.
                    Descriptor desc;
                    if (resp.Content.Headers.ContentLength == -1)
                    {
                        desc = await ResolveAsync(refDigest, cancellationToken);
                    }
                    else
                    {
                        desc = Repository.GenerateBlobDescriptor(resp, refDigest);
                    }

                    return (desc, await resp.Content.ReadAsStreamAsync());
                case HttpStatusCode.NotFound:
                    throw new NotFoundException();
                default:
                    throw await ErrorUtil.ParseErrorResponse(resp);
            }
        }

    }

}
