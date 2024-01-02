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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal class BlobStore : IBlobStore
{

    public Repository Repository { get; set; }

    public BlobStore(Repository repository)
    {
        Repository = repository;

    }


    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.RemoteReference;
        Digest.Validate(target.Digest);
        remoteReference.Reference = target.Digest;
        var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, remoteReference);
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
                throw await ErrorUtility.ParseErrorResponse(resp);
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
        using var resp = await Repository.HttpClient.PostAsync(url, null, cancellationToken);
        var reqHostname = resp.RequestMessage.RequestUri.Host;
        var reqPort = resp.RequestMessage.RequestUri.Port;
        if (resp.StatusCode != HttpStatusCode.Accepted)
        {
            throw await ErrorUtility.ParseErrorResponse(resp);
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
        var uri = new UriBuilder(location);
        var locationHostname = uri.Host;
        var locationPort = uri.Port;
        // if location port 443 is missing, add it back
        if (reqPort == 443 && locationHostname == reqHostname && locationPort != reqPort)
        {
            location = new UriBuilder($"{locationHostname}:{reqPort}").ToString();
        }

        url = location;

        var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentLength = expected.Size;

        // the expected media type is ignored as in the API doc.
        req.Content.Headers.Add("Content-Type", "application/octet-stream");

        // add digest key to query string with expected digest value
        req.RequestUri = new UriBuilder($"{req.RequestUri}?digest={expected.Digest}").Uri;

        //reuse credential from previous POST request
        resp.Headers.TryGetValues("Authorization", out var auth);
        if (auth != null)
        {
            req.Headers.Add("Authorization", auth.FirstOrDefault());
        }
        using var resp2 = await Repository.HttpClient.SendAsync(req, cancellationToken);
        if (resp2.StatusCode != HttpStatusCode.Created)
        {
            throw await ErrorUtility.ParseErrorResponse(resp2);
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
        var remoteReference = Repository.ParseReference(reference);
        var refDigest = remoteReference.Digest();
        var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, remoteReference);
        var requestMessage = new HttpRequestMessage(HttpMethod.Head, url);
        using var resp = await Repository.HttpClient.SendAsync(requestMessage, cancellationToken);
        return resp.StatusCode switch
        {
            HttpStatusCode.OK => Repository.GenerateBlobDescriptor(resp, refDigest),
            HttpStatusCode.NotFound => throw new NotFoundException($"{remoteReference.Reference}: not found"),
            _ => throw await ErrorUtility.ParseErrorResponse(resp)
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
        var refDigest = remoteReference.Digest();
        var url = URLUtiliity.BuildRepositoryBlobURL(Repository.PlainHTTP, remoteReference);
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
                throw await ErrorUtility.ParseErrorResponse(resp);
        }
    }
}
