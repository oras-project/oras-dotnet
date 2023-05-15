using Oras.Exceptions;
using Oras.Interfaces.Registry;
using Oras.Models;
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
using System.Web;

namespace Oras.Remote
{
    public class RepositoryOption : IRepositoryOption
    {
        public HttpClient Client { get; set; }
        public ReferenceObj Reference { get; set; }
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
        /// Client is the underlying HTTP client used to access the remote registry.
        /// </summary>
        public HttpClient Client { get; set; }

        /// <summary>
        /// ReferenceObj references the remote repository.
        /// </summary>
        public ReferenceObj Reference { get; set; }

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
            var refObj = new ReferenceObj().ParseReference(reference);
            Reference = refObj;
        }

        /// <summary>
        /// This constructor customizes the Properties using the values
        /// from the RepositoryOptions.
        /// RepositoryOptions contains unexported state that must not be copied
        /// to multiple Repositories. To handle this we explicitly copy only the
        /// fields that we want to reproduce.
        /// </summary>
        /// <param name="refObj"></param>
        /// <param name="option"></param>
        public Repository(ReferenceObj refObj, IRepositoryOption option)
        {
            refObj.ValidateRepository();
            Client = option.Client;
            Reference = refObj;
            PlainHTTP = option.PlainHTTP;
            ManifestMediaTypes = option.ManifestMediaTypes;
            TagListPageSize = option.TagListPageSize;
            MaxMetadataBytes = option.MaxMetadataBytes;

        }

        /// <summary>
        /// client returns an HTTP client used to access the remote repository.
        /// A default HTTP client is return if the client is not configured.
        /// </summary>
        /// <returns></returns>
        private HttpClient client()
        {
            if (Client is null)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", new string[] { "oras-dotnet" });
                return client;
            }

            return Client;
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
        public async Task<(Descriptor, Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
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
            await Manifests().FetchReferenceAsync(reference, cancellationToken);
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
                async (tag) =>
                {
                    res.AddRange(tag);

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
                var url = RegistryUtil.BuildRepositoryTagListURL(PlainHTTP, Reference);
                while (true)
                {
                    await tagsAsync(last, fn, url, cancellationToken);
                    last = "";
                }
            }
            catch (Exception e) when (!(e is NoLinkHeaderException))
            {

                throw e;

            }

        }

        /// <summary>
        /// tagsAsync returns a single page of tag list with the next link.
        /// </summary>
        /// <param name="last"></param>
        /// <param name="fn"></param>
        /// <param name="url"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> tagsAsync(string last, Action<string[]> fn, string url, CancellationToken cancellationToken)
        {
            if (TagListPageSize > 0 || last != "")
            {
                if (TagListPageSize > 0)
                {
                    url = url + "?n=" + TagListPageSize;
                }
                if (last != "")
                {
                    url = url + "&last=" + last;
                }
            }
            var resp = await Client.GetAsync(url, cancellationToken);
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw ErrorUtil.ParseErrorResponse(resp);

            }

            var data = await resp.Content.ReadAsStringAsync();
            var page = JsonSerializer.Deserialize<ResponseTypes.Tags>(data);
            fn(page.tags);
            return Utils.ParseLink(resp);

        }

        /// <summary>
        /// deleteAsync removes the content identified by the descriptor in the
        /// entity blobs or manifests.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="isManifest"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="Exception"></exception>
        internal async Task deleteAsync(Descriptor target, bool isManifest, CancellationToken cancellationToken)
        {
            var refObj = Reference;
            refObj.Reference = target.Digest;
            Func<bool, ReferenceObj, string> buildURL = RegistryUtil.BuildRepositoryBlobURL;
            if (isManifest)
            {
                buildURL = RegistryUtil.BuildRepositoryManifestURL;
            }

            var url = buildURL(PlainHTTP, refObj);
            var resp = await Client.DeleteAsync(url, cancellationToken);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.Accepted:
                    verifyContentDigest(resp, target.Digest);
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"digest {target.Digest} not found");
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);
                    break;
            }
        }


        /// <summary>
        /// verifyContentDigest verifies "Docker-Content-Digest" header if present.
        /// OCI distribution-spec states the Docker-Content-Digest header is optional.
        /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#legacy-docker-support-http-headers
        /// </summary>
        /// <param name="resp"></param>
        /// <param name="expected"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void verifyContentDigest(HttpResponseMessage resp, string expected)
        {
            var digestStr = resp.Headers.GetValues(DockerContentDigestHeader).FirstOrDefault();
            if (digestStr != null && !digestStr.Any())
            {
                return;
            }

            string contentDigest;
            try
            {
                contentDigest = DigestUtil.Parse(digestStr);

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
        public ReferenceObj ParseReference(string reference)
        {
            try
            {
                var refObj = new ReferenceObj().ParseReference(reference);
                if (refObj.Registry != Reference.Registry || refObj.Repository != Reference.Repository)
                {
                    throw new InvalidReferenceException(
                        $"mismatch between received {JsonSerializer.Serialize(refObj)} and expected {JsonSerializer.Serialize(Reference)}");
                }

                if (refObj.Reference.Length == 0)
                {
                    throw new InvalidReferenceException();
                }
                return refObj;
            }
            catch (Exception)
            {
                var refObj = new ReferenceObj
                {
                    Registry = Reference.Registry,
                    Repository = Reference.Repository,
                    Reference = Reference.Reference
                };
                //reference is not a FQDN
                if (reference.IndexOf("@") is var index && index != 1)
                {
                    // `@` implies *digest*, so drop the *tag* (irrespective of what it is).
                    refObj.Reference = reference.Substring(index + 1);
                    refObj.ValidateReferenceAsDigest();
                }
                else
                {
                    refObj.ValidateReference();
                }
                return refObj;
            }

        }


    }

    public class ManifestStore : IManifestStore
    {
        public Repository Repo { get; set; }
        public ManifestStore(Repository repository)
        {
            Repo = repository;

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
            var refObj = Repo.Reference;
            refObj.Reference = target.Digest;
            var url = RegistryUtil.BuildRepositoryManifestURL(Repo.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", target.MediaType);
            var resp = await Repo.Client.SendAsync(req, cancellationToken);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"digest {target.Digest} not found");
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);
            }
            var mediaType = resp.Content.Headers.ContentType.MediaType;
            if (mediaType != target.MediaType)
            {
                throw new Exception(
                    $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch response Content-Type {mediaType}: expect {target.MediaType}");
            }
            if (resp.Content.Headers.ContentLength is var size && size != -1 && size != target.Size)
            {
                throw new Exception(
                    $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch Content-Length");
            }
            ReferenceObj.VerifyContentDigest(resp, target.Digest);
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
            catch (NotFoundException e)
            {
                return false;
            }

            return false;

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
            await pushWithIndexing(expected, content, expected.Digest, cancellationToken);
        }

        /// <summary>
        /// pushWithIndexing pushes the manifest content matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="r"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        private async Task pushWithIndexing(Descriptor expected, Stream r, string reference, CancellationToken cancellationToken)
        {
            await pushAsync(expected, r, reference, cancellationToken);
            return;
        }

        /// <summary>
        /// pushAsync pushes the manifest content, matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="stream"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        private async Task pushAsync(Descriptor expected, Stream stream, string reference, CancellationToken cancellationToken)
        {
            var refObj = Repo.Reference;
            refObj.Reference = reference;
            // pushing usually requires both pull and push actions.
            // Reference: https://github.com/distribution/distribution/blob/v2.7.1/registry/handlers/app.go#L921-L930
            var url = RegistryUtil.BuildRepositoryManifestURL(Repo.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Content = new StreamContent(stream);
            if (req.Content != null && req.Content.Headers.ContentLength != expected.Size)
            {
                // short circuit a size mismatch for built-in types
                throw new Exception(
                    $"{req.Method} {req.RequestUri}: mismatch Content-Length: expect {expected.Size}");
            }
            req.Content.Headers.ContentLength = expected.Size;
            req.Content.Headers.Add("Content-Type", expected.MediaType);

            // if the underlying client is an auth client, the content might be read
            // more than once for obtaining the auth challenge and the actual request.
            // To prevent double reading, the manifest is read and stored in the memory,
            // and serve from the memory.
            var client = Repo.Client;
            var resp = await client.SendAsync(req, cancellationToken);
            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw ErrorUtil.ParseErrorResponse(resp);
            }
            ReferenceObj.VerifyContentDigest(resp, expected.Digest);
        }

        /// <summary>
        /// LimitSize returns ErrSizeExceedsLimit if the size of desc exceeds the limit n.
        /// If n is less than or equal to zero, defaultMaxMetadataBytes is used.
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="n"></param>
        /// <exception cref="SizeExceedsLimitException"></exception>
        private void LimitSize(Descriptor desc, long n)
        {
            if (n <= 0)
            {
                n = Repo.defaultMaxMetaBytes;
            }

            if (desc.Size > n)
            {
                throw new SizeExceedsLimitException($"content size {desc.Size} exceeds MaxMetadataBytes {n}");
            }

            return;
        }

        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repo.ParseReference(reference);
            var url = RegistryUtil.BuildRepositoryManifestURL(Repo.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            req.Headers.Add("Accept", ManifestUtil.ManifestAcceptHeader(Repo.ManifestMediaTypes));
            var res = await Repo.Client.SendAsync(req, cancellationToken);

            switch (res.StatusCode)
            {
                case HttpStatusCode.OK:
                    return generateDescriptor(res, refObj, req.Method);
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"reference {reference} not found");
                default:
                    throw ErrorUtil.ParseErrorResponse(res);
            }
        }

        /// <summary>
        ///  generateDescriptor returns a descriptor generated from the response.
        /// </summary>
        /// <param name="res"></param>
        /// <param name="refObj"></param>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Descriptor generateDescriptor(HttpResponseMessage res, ReferenceObj refObj, HttpMethod httpMethod)
        {
            string mediaType;
            try
            {
                // 1. Validate Content-Type
                mediaType = res.Content.Headers.ContentType.MediaType;
                MediaTypeHeaderValue.TryParse(mediaType.ToString(), out var parsedMediaType);

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
            var refDigest = refObj.Digest();
            ReferenceObj.VerifyContentDigest(res, refObj.Digest());

            // 4. Validate Server Digest (if present)
            var serverHeaderDigest = res.Headers.GetValues("Docker-Content-Digest").FirstOrDefault();
            if (serverHeaderDigest != null)
            {
                try
                {
                    ReferenceObj.VerifyContentDigest(res, serverHeaderDigest);
                }
                catch (Exception)
                {
                    throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: invalid response header value: `Docker-Content-Digest: {serverHeaderDigest}`");
                }
            }

            // 5. Now, look for specific error conditions;
            var contentDigest = string.Empty;

            if (serverHeaderDigest.Length == 0)
            {
                if (httpMethod == HttpMethod.Head)
                {
                    if (refDigest.Length == 0)
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
                        calculatedDigest = calculateDigestFromResponse(res, Repo.MaxMetadataBytes);
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
                contentDigest = serverHeaderDigest;
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
            try
            {
                byte[] content = Utils.LimitReader(res.Content, maxMetadataBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: failed to read response body: {ex.Message}");
            }
            return DigestUtil.FromBytes(res.Content);
        }

        /// <summary>
        /// DeleteAsync removes the manifest content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            deleteWithIndexing(target, cancellationToken);
        }

        /// <summary>
        ///  deleteWithIndexing removes the manifest content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task deleteWithIndexing(Descriptor target, CancellationToken cancellationToken)
        {
            await Repo.deleteAsync(target, true, cancellationToken);
        }

        /// <summary>
        /// FetchReferenceAsync fetches the manifest identified by the reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(Descriptor, Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repo.ParseReference(reference);
            var url = RegistryUtil.BuildRepositoryManifestURL(Repo.PlainHTTP, refObj);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Content.Headers.Add("Accept", ManifestUtil.ManifestAcceptHeader(Repo.ManifestMediaTypes));
            var resp = await Repo.Client.SendAsync(req, cancellationToken);
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
                        desc = generateDescriptor(resp, refObj, HttpMethod.Get);
                    }
                    return (desc, await resp.Content.ReadAsStreamAsync());
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{req.Method} {req.RequestUri}: manifest unknown");
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);

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
            var refObj = Repo.ParseReference(reference);
            await pushWithIndexing(expected, content, refObj.Reference, cancellationToken);
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
            var refObj = Repo.ParseReference(reference);
            var rc = await FetchAsync(descriptor, cancellationToken);
            await pushAsync(descriptor, rc, refObj.Reference, cancellationToken);
        }
    }

    public class BlobStore : IBlobStore
    {

        public Repository Repo { get; set; }

        public BlobStore(Repository repository)
        {
            Repo = repository;

        }


        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var refObj = Repo.Reference;
            refObj.Reference = target.Digest;
            var url = RegistryUtil.BuildRepositoryBlobURL(Repo.PlainHTTP, refObj);
            var resp = await Repo.Client.GetAsync(url, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    // server does not support seek as `Range` was ignored.
                    if (resp.Content.Headers.ContentLength is var size && size != -1 && size != target.Size)
                    {
                        throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: mismatch Content-Length");
                    }

                    // check server range request capability.
                    // Docker spec allows range header form of "Range: bytes=<start>-<end>".
                    // However, the remote server may still not RFC 7233 compliant.
                    // Reference: https://docs.docker.com/registry/spec/api/#blob
                    if (resp.Headers.GetValues("Accept-Ranges").FirstOrDefault() == "bytes")
                    {
                        var stream = new MemoryStream();
                        long from = 0;
                        // make request using ranges until the whole data is read
                        while (from < target.Size)
                        {

                            var to = from + 1024 * 1024 - 1;
                            if (to > target.Size)
                            {
                                to = target.Size;
                            }
                            Repo.Client.DefaultRequestHeaders.Range = new RangeHeaderValue(from, to);
                            resp = await Repo.Client.GetAsync(url, cancellationToken);
                            if (resp.StatusCode != HttpStatusCode.PartialContent)
                            {
                                throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response status code: {resp.StatusCode}");
                            }
                            await resp.Content.CopyToAsync(stream);
                            from = to + 1;
                        }
                        stream.Seek(0, SeekOrigin.Begin);
                        return stream;
                    }

                    return await resp.Content.ReadAsStreamAsync();
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{target.Digest}: not found");
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);
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
            catch (Exception ex) when (ex is NotFoundException)
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
            var url = RegistryUtil.BuildRepositoryBlobUploadURL(Repo.PlainHTTP, Repo.Reference);
            var resp = await Repo.Client.PostAsync(url, null, cancellationToken);
            var reqHostname = resp.RequestMessage.RequestUri.Host;
            var reqPort = resp.RequestMessage.RequestUri.Port;
            if (resp.StatusCode != HttpStatusCode.Accepted)
            {
                throw ErrorUtil.ParseErrorResponse(resp);
            }

            // monolithic upload
            var location = resp.Headers.Location;
            // work-around solution for https://github.com/oras-project/oras-go/issues/177
            // For some registries, if the port 443 is explicitly set to the hostname
            // like registry.wabbit-networks.io:443/myrepo, blob push will fail since
            // the hostname of the Location header in the response is set to
            // registry.wabbit-networks.io instead of registry.wabbit-networks.io:443.

            var locationHostname = location.Host;
            var locationPort = location.Port;
            // if location port 443 is missing, add it back
            if (reqPort == 443 && locationHostname == reqHostname && locationPort != reqPort)
            {
                location = new Uri($"{locationHostname}:{reqPort}");
            }
            url = location.ToString();

            var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Add("Content-Type", "application/octet-stream");
            req.Headers.Add("Content-Length", content.Length.ToString());
            req.Content = new StreamContent(content);

            if (req.Content != null && req.Content.Headers.ContentLength is var size && size != expected.Size)
            {
                throw new Exception($"mismatch content length {size}: expect {expected.Size}");
            }
            req.Content.Headers.ContentLength = expected.Size;

            // the expected media type is ignored as in the API doc.
            req.Headers.Add("Content-Type", "application/octet-stream");
            // add digest key to query string with expected digest value
            var query = HttpUtility.ParseQueryString(location.Query);
            query.Add("digest", expected.Digest);
            req.RequestUri = new UriBuilder(location)
            {
                Query = query.ToString()
            }.Uri;

            //reuse credential from previous POST request
            var auth = resp.Headers.GetValues("Authorization").FirstOrDefault();
            if (auth != null)
            {
                req.Headers.Add("Authorization", auth);
            }
            resp = await Repo.Client.SendAsync(req, cancellationToken);
            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw ErrorUtil.ParseErrorResponse(resp);
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
            var refObj = Repo.ParseReference(reference);
            var refDigest = refObj.Digest();
            var url = RegistryUtil.BuildRepositoryBlobURL(Repo.PlainHTTP, refObj);
            var resp = await Repo.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    return GenerateBlobDescriptor(resp, refDigest);

                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{refObj.Reference}: not found");
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);
            }
        }

        /// <summary>
        /// DeleteAsync deletes the content identified by the given descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            await Repo.deleteAsync(target, false, cancellationToken);
        }

        /// <summary>
        /// FetchReferenceAsync fetches the blob identified by the reference.
        /// The reference must be a digest.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(Descriptor, Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            var refObj = Repo.ParseReference(reference);
            var refDigest = refObj.Digest();
            var url = RegistryUtil.BuildRepositoryBlobURL(Repo.PlainHTTP, refObj);
            var resp = await Repo.Client.GetAsync(url, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.Accepted:
                    // server does not support seek as `Range` was ignored.
                    Descriptor desc = null;
                    if (resp.Content.Headers.ContentLength == -1)
                    {
                        desc = await ResolveAsync(refDigest, cancellationToken);
                    }
                    else
                    {
                        desc = GenerateBlobDescriptor(resp, refDigest);
                    }
                    // check server range request capability.
                    // Docker spec allows range header form of "Range: bytes=<start>-<end>".
                    // However, the remote server may still not RFC 7233 compliant.
                    // Reference: https://docs.docker.com/registry/spec/api/#blob
                    if (resp.Headers.GetValues("Accept-Ranges").FirstOrDefault() == "bytes")
                    {
                        var stream = new MemoryStream();
                        // make request using ranges until the whole data is read
                        long from = 0;

                        while (from < desc.Size)
                        {
                            var to = from + 1024 * 1024 - 1;
                            if (to > desc.Size)
                            {
                                to = desc.Size;
                            }
                            Repo.Client.DefaultRequestHeaders.Range = new RangeHeaderValue(from, to);
                            resp = await Repo.Client.GetAsync(url, cancellationToken);
                            if (resp.StatusCode != HttpStatusCode.PartialContent)
                            {
                                throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response status code: {resp.StatusCode}");
                            }
                            await resp.Content.CopyToAsync(stream);
                            from = to + 1;
                        }
                        stream.Seek(0, SeekOrigin.Begin);
                        return (desc, stream);
                    }
                    return (desc, await resp.Content.ReadAsStreamAsync());
                case HttpStatusCode.NotFound:
                    throw new NotFoundException();
                default:
                    throw ErrorUtil.ParseErrorResponse(resp);

            }
        }

        /// <summary>
        /// GenerateBlobDescriptor returns a descriptor generated from the response.
        /// </summary>
        /// <param name="resp"></param>
        /// <param name="refDigest"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Descriptor GenerateBlobDescriptor(HttpResponseMessage resp, string refDigest)
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

            ReferenceObj.VerifyContentDigest(resp, refDigest);

            return new Descriptor
            {
                MediaType = mediaType,
                Digest = refDigest,
                Size = size
            };
        }
    }
}
