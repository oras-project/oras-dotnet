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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OrasProject.Oras.Registry.Remote;

public class BlobStore(Repository repository) : IBlobStore, IMounter
{
    public Repository Repository { get; init; } = repository;

    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReferenceFromDigest(target.Digest);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        var response = await Repository.Options.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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
                    return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
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
    /// FetchReferenceAsync fetches the blob identified by the reference.
    /// The reference must be a digest.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        var response = await Repository.Options.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Descriptor desc;
                    if (response.Content.Headers.ContentLength == null)
                    {
                        desc = await ResolveAsync(refDigest, cancellationToken).ConfigureAwait(false);
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
        var url = new UriFactory(Repository.Options).BuildRepositoryBlobUpload();
        using (var response = await Repository.Options.HttpClient.PostAsync(url, null, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }

            var location = response.Headers.Location ?? throw new HttpRequestException("missing location header");
            url = location.IsAbsoluteUri ? location : new Uri(url, location);
        }

        await CompletePushAsync(url, expected, content, cancellationToken);
    }

    /// <summary>
    /// ResolveAsync resolves a reference to a descriptor.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        var requestMessage = new HttpRequestMessage(HttpMethod.Head, url);
        using var resp = await Repository.Options.HttpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        return resp.StatusCode switch
        {
            HttpStatusCode.OK => resp.GenerateBlobDescriptor(refDigest),
            HttpStatusCode.NotFound => throw new NotFoundException($"{remoteReference.ContentReference}: not found"),
            _ => throw await resp.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false)
        };
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
        Func<CancellationToken, Task<Stream>>? getContent, CancellationToken cancellationToken)
    {
        var url = new UriFactory(Repository.Options).BuildRepositoryBlobUpload();
        var mountReq = new HttpRequestMessage(HttpMethod.Post, new UriBuilder(url)
        {
            Query =
                $"{url.Query}&mount={HttpUtility.UrlEncode(descriptor.Digest)}&from={HttpUtility.UrlEncode(fromRepository)}"
        }.Uri);

        using (var response = await Repository.Options.HttpClient.SendAsync(mountReq, cancellationToken)
                   .ConfigureAwait(false))
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
        // [spec]: https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#mounting-a-blob-from-another-repository

        async Task<Stream> GetContentStream()
        {
            Stream stream;
            if (getContent != null)
            {
                stream = await getContent(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var referenceOptions = repository.Options with
                {
                    Reference = Reference.Parse(fromRepository),
                };
                stream = await new Repository(referenceOptions).FetchAsync(descriptor, cancellationToken).ConfigureAwait(false);
            }

            return stream;
        }

        await using (var contents = await GetContentStream())
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
        CancellationToken cancellationToken)
    {
        // monolithic upload
        // add digest key to query string with descriptor digest value
        var req = new HttpRequestMessage(HttpMethod.Put, new UriBuilder(url)
        {
            Query = $"{url.Query}&digest={HttpUtility.UrlEncode(descriptor.Digest)}"
        }.Uri);
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentLength = descriptor.Size;

        // the descriptor media type is ignored as in the API doc.
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

        using (var response =
               await Repository.Options.HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
