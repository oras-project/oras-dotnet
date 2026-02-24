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
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Registry.Remote;

public class BlobStore(Repository repository) : IBlobStore, IBlobLocationProvider, IMounter
{
    private Repository Repository { get; } = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>
    /// FetchAsync fetches the blob by the given Descriptor target
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="HttpIOException"></exception>
    /// <exception cref="NotFoundException"></exception>
    /// <exception cref="Exceptions.ResponseException"></exception>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull);
        var remoteReference = Repository.ParseReferenceFromDigest(target.Digest);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var size = response.Content.Headers.ContentLength;
                    if (size != null && size != target.Size)
                    {
                        throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: mismatch Content-Length");
                    }

                    response.VerifyContentDigest(target.Digest);
                    return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{target.Digest}: not found");
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// FetchAsync fetches the blob identified by the reference.
    /// The reference must be a digest.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
        => await FetchAsync(reference, options: new FetchOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// FetchAsync fetches the blob identified by the reference with additional options.
    /// The reference must be a digest.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="options">Options for the fetch operation.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a custom header is a content header.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when a custom header name or value has an invalid format.
    /// </exception>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(
        string reference,
        FetchOptions options,
        CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull);
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        using var request = new HttpRequestMessage(HttpMethod.Get, url).AddHeaders(options.Headers);
        var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Descriptor desc;
                    if (response.Content.Headers.ContentLength == null)
                    {
                        var resolveOptions = new ResolveOptions { Headers = options.Headers };
                        desc = await ResolveAsync(refDigest, resolveOptions, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        desc = response.GenerateBlobDescriptor(refDigest);
                    }
                    return (desc, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                case HttpStatusCode.NotFound:
                    throw new NotFoundException();
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            response.Dispose();
            throw;
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
            await ResolveAsync(target.Digest, cancellationToken).ConfigureAwait(false);
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
        // pushing usually requires both pull and push actions.
        // Reference: https://github.com/distribution/distribution/blob/v2.7.1/registry/handlers/app.go#L921-L930
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull, Scope.Action.Push);
        var url = new UriFactory(Repository.Options).BuildRepositoryBlobUpload();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        using (var response = await Repository.Options.Client.SendAsync(
            requestMessage,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }

            var location = response.Headers.Location ?? throw new HttpRequestException("missing location header");
            url = location.IsAbsoluteUri ? location : new Uri(url, location);
        }

        await CompletePushAsync(url, expected, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ResolveAsync resolves a reference to a descriptor.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        => await ResolveAsync(reference, new ResolveOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// ResolveAsync resolves a reference to a descriptor with additional options.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="options">Options for the resolve operation.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a custom header is a content header.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when a custom header name or value has an invalid format.
    /// </exception>
    public async Task<Descriptor> ResolveAsync(
        string reference,
        ResolveOptions options,
        CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull);
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Head, url).AddHeaders(options.Headers);
        using var resp = await Repository.Options.Client.SendAsync(
            requestMessage,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return resp.StatusCode switch
        {
            HttpStatusCode.OK => resp.GenerateBlobDescriptor(refDigest),
            HttpStatusCode.NotFound => throw new NotFoundException($"{remoteReference.ContentReference}: not found"),
            _ => throw await resp.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// GetBlobLocationAsync retrieves the location URL for a blob without downloading its content.
    /// Most OCI Distribution Spec v1.1.1 registries return a redirect with a blob location in the header
    /// instead of returning the content directly on a /v2/<name>/blobs/<digest> request.
    /// This method makes an authenticated request without following redirects to capture the location URL.
    /// 
    /// Returns null if the registry returns the content directly (HTTP 200) instead of a redirect.
    /// 
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#pulling-blobs
    /// </summary>
    /// <param name="target">The descriptor identifying the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blob location URL if a redirect is returned, otherwise null</returns>
    /// <exception cref="ArgumentException">Thrown when the provided HttpClient has AllowAutoRedirect enabled</exception>
    /// <exception cref="HttpIOException">Thrown when the response is invalid</exception>
    /// <exception cref="NotFoundException">Thrown when the blob is not found</exception>
    /// <exception cref="Exceptions.ResponseException">Thrown when the request fails</exception>
    public async Task<Uri?> GetBlobLocationAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull);
        var remoteReference = Repository.ParseReferenceFromDigest(target.Digest);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Repository.Options.Client.SendAsync(
            request,
            partitionId: Repository.Options.PartitionId,
            allowAutoRedirect: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Validate that the client didn't follow redirects automatically.
        // If response.RequestMessage.RequestUri differs from the original request URI,
        // it means the provided HttpClient had AllowAutoRedirect=true, which breaks this API.
        var requestUri = response.RequestMessage?.RequestUri;
        if (requestUri != null &&
            !string.Equals(requestUri.ToString(), url.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The provided HttpClient automatically followed a redirect, which is incompatible with GetBlobLocationAsync. " +
                $"The custom HttpClient must be configured with an HttpClientHandler that has AllowAutoRedirect = false.");
        }

        switch (response.StatusCode)
        {
            // Handle all HTTP redirect status codes for maximum registry compatibility.
            // The OCI Distribution Spec doesn't mandate specific redirect codes - it only requires
            // the final response to be 200 OK. However, real-world registries use redirects to offload
            // blob storage to cloud backends (Azure Blob Storage, S3, GCS, etc.):
            // - 307 (TemporaryRedirect): Most common (ACR, ECR, GCR) - preserves GET method
            // - 302 (Found): Older registry implementations (pre-2016 Docker Registry v2)
            // - 303 (SeeOther): Some CDN-backed registries - explicit "see other" semantics
            // - 301/308 (Permanent): Rare, but HTTP spec allows them
            // Supporting all redirect codes ensures compatibility across registry implementations.
            case HttpStatusCode.MovedPermanently:       // 301
            case HttpStatusCode.Found:                  // 302
            case HttpStatusCode.SeeOther:               // 303
            case HttpStatusCode.TemporaryRedirect:      // 307
            case HttpStatusCode.PermanentRedirect:      // 308
                {
                    // Verify the digest in the response headers to ensure redirect points to correct blob
                    response.VerifyContentDigest(target.Digest);

                    // Get and validate the Location header
                    var location = response.Headers.Location;
                    if (location == null)
                    {
                        throw new HttpIOException(HttpRequestError.InvalidResponse,
                            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: redirect response missing Location header");
                    }

                    // Require absolute URI to avoid cross-domain ambiguity.
                    // This constraint may be removed in the future if needed.
                    if (!location.IsAbsoluteUri)
                    {
                        throw new HttpIOException(HttpRequestError.InvalidResponse,
                            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: redirect Location header must be an absolute URI");
                    }

                    // Validate HTTPS unless PlainHttp is explicitly allowed
                    if (!Repository.Options.PlainHttp && location.Scheme != "https")
                    {
                        throw new HttpIOException(HttpRequestError.InvalidResponse,
                            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: redirect location must use HTTPS, got {location.Scheme}://{location.Host}");
                    }

                    return location;
                }

            case HttpStatusCode.OK:
                // Registry returned content directly, no redirect
                response.VerifyContentDigest(target.Digest);
                return null;

            case HttpStatusCode.NotFound:
                throw new NotFoundException($"{target.Digest}: not found");

            default:
                throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// DeleteAsync deletes the content identified by the given descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await Repository.DeleteAsync(target, false, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Mounts the given descriptor from fromRepository into the blob store.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="fromRepository"></param>
    /// <param name="getContent"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="HttpRequestException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task MountAsync(Descriptor descriptor, string fromRepository,
        Func<CancellationToken, Task<Stream>>? getContent, CancellationToken cancellationToken = default)
    {
        // pushing usually requires both pull and push actions.
        // Reference: https://github.com/distribution/distribution/blob/v2.7.1/registry/handlers/app.go#L921-L930
        ScopeManager.SetActionsForRepository(Repository.Options.Client, Repository.Options.Reference, Scope.Action.Pull, Scope.Action.Push);

        // We also need pull access to the source repo.
        if (Reference.TryParse(fromRepository, out var fromReference))
        {
            ScopeManager.SetActionsForRepository(Repository.Options.Client, fromReference, Scope.Action.Pull);
        }

        var url = new UriFactory(Repository.Options).BuildRepositoryBlobUpload();
        using var mountReq = new HttpRequestMessage(HttpMethod.Post, new UriBuilder(url)
        {
            Query =
                $"{url.Query}&mount={HttpUtility.UrlEncode(descriptor.Digest)}&from={HttpUtility.UrlEncode(fromRepository)}"
        }.Uri);

        using (var response = await Repository.Options.Client.SendAsync(
            mountReq,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                    // 201, layer has been mounted
                    response.VerifyContentDigest(descriptor.Digest);
                    return;
                case HttpStatusCode.Accepted:
                    {
                        // 202, mounting failed. upload session has begun
                        var location = response.Headers.Location ??
                                        throw new HttpRequestException("missing location header");
                        url = location.IsAbsoluteUri ? location : new Uri(url, location);
                        break;
                    }
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // From the [spec]:
        //
        // "If a registry does not support cross-repository mounting
        // or is unable to mount the requested blob,
        // it SHOULD return a 202.
        // This indicates that the upload session has begun
        // and that the client MAY proceed with the upload."
        //
        // So we need to get the content from somewhere in order to
        // push it. If the caller has provided a getContent function, we
        // can use that, otherwise pull the content from the source repository.
        //
        // [spec]: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#mounting-a-blob-from-another-repository

        async Task<Stream> GetContentStream()
        {
            Stream stream;
            if (getContent != null)
            {
                stream = await getContent(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var referenceOptions = Repository.Options with
                {
                    Reference = Reference.Parse(fromRepository),
                };
                stream = await new Repository(referenceOptions).FetchAsync(descriptor, cancellationToken).ConfigureAwait(false);
            }

            return stream;
        }

        var contents = await GetContentStream().ConfigureAwait(false);
        await using (contents.ConfigureAwait(false))
        {
            await CompletePushAsync(url, descriptor, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Completes a push operation started beforehand.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="descriptor"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="Exception"></exception>
    private async Task CompletePushAsync(Uri url, Descriptor descriptor, Stream content,
        CancellationToken cancellationToken = default)
    {
        // monolithic upload
        // add digest key to query string with descriptor digest value
        using var req = new HttpRequestMessage(HttpMethod.Put, new UriBuilder(url)
        {
            Query = $"{url.Query}&digest={HttpUtility.UrlEncode(descriptor.Digest)}"
        }.Uri);
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentLength = descriptor.Size;

        // the descriptor media type is ignored as in the API doc.
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

        using var response = await Repository.Options.Client.SendAsync(
            req,
            partitionId: Repository.Options.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
