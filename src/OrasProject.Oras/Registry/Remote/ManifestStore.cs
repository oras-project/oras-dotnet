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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

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
        var remoteReference = Repository._opts.Reference;
        remoteReference.ContentReference = target.Digest;
        var url = new UriFactory(remoteReference, Repository._opts.PlainHttp).BuildRepositoryManifest();
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Accept", target.MediaType);
        var resp = await Repository._opts.HttpClient.SendAsync(req, cancellationToken);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"digest {target.Digest} not found");
            default:
                throw await ErrorUtility.ParseErrorResponse(resp);
        }
        var mediaType = resp.Content.Headers?.ContentType.MediaType;
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
        Repository.VerifyContentDigest(resp, target.Digest);
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
        await InternalPushAsync(expected, content, expected.Digest, cancellationToken);
    }


    /// <summary>
    /// PushAsync pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="stream"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    private async Task InternalPushAsync(Descriptor expected, Stream stream, string reference, CancellationToken cancellationToken)
    {
        var remoteReference = Repository._opts.Reference;
        remoteReference.ContentReference = reference;
        var url = new UriFactory(remoteReference, Repository._opts.PlainHttp).BuildRepositoryManifest();
        var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = new StreamContent(stream);
        req.Content.Headers.ContentLength = expected.Size;
        req.Content.Headers.Add("Content-Type", expected.MediaType);
        var client = Repository._opts.HttpClient;
        using var resp = await client.SendAsync(req, cancellationToken);
        if (resp.StatusCode != HttpStatusCode.Created)
        {
            throw await ErrorUtility.ParseErrorResponse(resp);
        }
        Repository.VerifyContentDigest(resp, expected.Digest);
    }

    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository._opts.PlainHttp).BuildRepositoryManifest();
        var req = new HttpRequestMessage(HttpMethod.Head, url);
        req.Headers.Add("Accept", ManifestUtility.ManifestAcceptHeader(Repository._opts.ManifestMediaTypes));
        using var res = await Repository._opts.HttpClient.SendAsync(req, cancellationToken);

        return res.StatusCode switch
        {
            HttpStatusCode.OK => await GenerateDescriptorAsync(res, remoteReference, req.Method),
            HttpStatusCode.NotFound => throw new NotFoundException($"reference {reference} not found"),
            _ => throw await ErrorUtility.ParseErrorResponse(res)
        };
    }

    /// <summary>
    /// GenerateDescriptor returns a descriptor generated from the response.
    /// </summary>
    /// <param name="res"></param>
    /// <param name="reference"></param>
    /// <param name="httpMethod"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<Descriptor> GenerateDescriptorAsync(HttpResponseMessage res, Reference reference, HttpMethod httpMethod)
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
        if (!res.Content.Headers.ContentLength.HasValue || res.Content.Headers.ContentLength == -1)
        {
            throw new Exception($"{res.RequestMessage.Method} {res.RequestMessage.RequestUri}: unknown response Content-Length");
        }

        // 3. Validate Client Reference
        string refDigest = string.Empty;
        try
        {
            refDigest = reference.Digest;
        }
        catch (Exception)
        {
        }


        // 4. Validate Server Digest (if present)
        res.Content.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string> serverHeaderDigest);
        var serverDigest = serverHeaderDigest?.First();
        if (!string.IsNullOrEmpty(serverDigest))
        {
            try
            {
                Repository.VerifyContentDigest(res, serverDigest);
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
                    calculatedDigest = await CalculateDigestFromResponse(res);
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
        if (!string.IsNullOrEmpty(refDigest) && refDigest != contentDigest)
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
    static async Task<string> CalculateDigestFromResponse(HttpResponseMessage res)
    {
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return Digest.ComputeSHA256(bytes);
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
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository._opts.PlainHttp).BuildRepositoryManifest();
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Accept", ManifestUtility.ManifestAcceptHeader(Repository._opts.ManifestMediaTypes));
        var resp = await Repository._opts.HttpClient.SendAsync(req, cancellationToken);
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
                    desc = await GenerateDescriptorAsync(resp, remoteReference, HttpMethod.Get);
                }

                return (desc, await resp.Content.ReadAsStreamAsync());
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"{req.Method} {req.RequestUri}: manifest unknown");
            default:
                throw await ErrorUtility.ParseErrorResponse(resp);

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
    public async Task PushAsync(Descriptor expected, Stream content, string reference,
        CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        await InternalPushAsync(expected, content, remoteReference.ContentReference, cancellationToken);
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
        var remoteReference = Repository.ParseReference(reference);
        var rc = await FetchAsync(descriptor, cancellationToken);
        await InternalPushAsync(descriptor, rc, remoteReference.ContentReference, cancellationToken);
    }
}
