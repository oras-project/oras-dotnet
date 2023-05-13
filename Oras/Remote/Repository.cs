﻿using Oras.Exceptions;
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
        public const string dockerContentDigestHeader = "Docker-Content-Digest";

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
        public Repository(ReferenceObj refObj, RepositoryOption option)
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
            if (ManifestUtil.isManifest(ManifestMediaTypes, desc))
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
                String.Empty,
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
              throw  ErrorUtil.ParseErrorResponse(resp);

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
            var digestStr = resp.Headers.GetValues(dockerContentDigestHeader).FirstOrDefault();
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
                   $"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response header: {dockerContentDigestHeader}: {digestStr}"
                    );
            }

            if (contentDigest != expected)
            {
                throw new Exception(
$"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response; digest mismatch in {dockerContentDigestHeader}: received {contentDigest}, while expecting {expected}"
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

        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<(Descriptor, Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task PushReferenceAsync(Descriptor descriptor, Stream content, string reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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

            catch (Exception ex)
            {
                throw ex;
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
                    return generateBlobDescriptor(resp, refDigest);
                    
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
                        desc = generateBlobDescriptor(resp, refDigest);
                    }
                    // check server range request capability.
                    // Docker spec allows range header form of "Range: bytes=<start>-<end>".
                    // However, the remote server may still not RFC 7233 compliant.
                    // Reference: https://docs.docker.com/registry/spec/api/#blob
                    if (resp.Headers.GetValues("Accept-Ranges").FirstOrDefault()  == "bytes")
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
        /// generateBlobDescriptor returns a descriptor generated from the response.
        /// </summary>
        /// <param name="resp"></param>
        /// <param name="refDigest"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Descriptor generateBlobDescriptor(HttpResponseMessage resp, string refDigest)
        {
            var mediaType = resp.Content.Headers.ContentType.MediaType;
            if (String.IsNullOrEmpty(mediaType))
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
