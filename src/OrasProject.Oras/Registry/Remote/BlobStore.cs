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

namespace OrasProject.Oras.Registry.Remote;

public class BlobStore : IBlobStore
{
    public Repository Repository { get; set; }

    public BlobStore(Repository repository)
    {
        Repository = repository;
    }

    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.Options.Reference;
        Digest.Validate(target.Digest);
        remoteReference.ContentReference = target.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        var resp = await Repository.Options.HttpClient.GetAsync(url, cancellationToken);
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
                throw await resp.ParseErrorResponseAsync(cancellationToken);
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
        var url = new UriFactory(Repository.Options).BuildRepositoryBlobUpload();
        using (var resp = await Repository.Options.HttpClient.PostAsync(url, null, cancellationToken))
        {
            if (resp.StatusCode != HttpStatusCode.Accepted)
            {
                throw await resp.ParseErrorResponseAsync(cancellationToken);
            }

            var location = resp.Headers.Location ?? throw new HttpRequestException("missing location header");
            url = location.IsAbsoluteUri ? location : new Uri(url, location);
        }

        // monolithic upload
        // add digest key to query string with expected digest value
        var req = new HttpRequestMessage(HttpMethod.Put, new UriBuilder(url)
        {
            Query = $"digest={HttpUtility.UrlEncode(expected.Digest)}"
        }.Uri);
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentLength = expected.Size;

        // the expected media type is ignored as in the API doc.
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

        using (var resp = await Repository.Options.HttpClient.SendAsync(req, cancellationToken))
        {
            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw await resp.ParseErrorResponseAsync(cancellationToken);
            }
        }
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
        using var resp = await Repository.Options.HttpClient.SendAsync(requestMessage, cancellationToken);
        return resp.StatusCode switch
        {
            HttpStatusCode.OK => GenerateBlobDescriptor(resp, refDigest),
            HttpStatusCode.NotFound => throw new NotFoundException($"{remoteReference.ContentReference}: not found"),
            _ => throw await resp.ParseErrorResponseAsync(cancellationToken)
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
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest;
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryBlob();
        var resp = await Repository.Options.HttpClient.GetAsync(url, cancellationToken);
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
                    desc = GenerateBlobDescriptor(resp, refDigest);
                }

                return (desc, await resp.Content.ReadAsStreamAsync());
            case HttpStatusCode.NotFound:
                throw new NotFoundException();
            default:
                throw await resp.ParseErrorResponseAsync(cancellationToken);
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
        var size = resp.Content.Headers.ContentLength.Value;
        if (size == -1)
        {
            throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: unknown response Content-Length");
        }

        resp.VerifyContentDigest(refDigest);

        return new Descriptor
        {
            MediaType = mediaType,
            Digest = refDigest,
            Size = size
        };
    }
}
