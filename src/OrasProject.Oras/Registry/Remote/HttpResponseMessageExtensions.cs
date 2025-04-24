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
using OrasProject.Oras.Oci;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal static class HttpResponseMessageExtensions
{
    private const string _dockerContentDigestHeader = "Docker-Content-Digest";

    /// <summary>
    /// Parses the error returned by the remote registry.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ResponseException> ParseErrorResponseAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ResponseException(response, body);
    }
    
    /// <summary>
    /// Parses the error returned by the remote registry.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ResponseException> ParseErrorResponseAsync(this HttpResponseMessage response, string message, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ResponseException(response, body, message);
    }

    /// <summary>
    /// Returns the URL of the response's "Link" header, if present.
    ///  The link header is of the form <link>; rel="next"
    /// </summary>
    /// <returns>next link or null if not present</returns>
    public static Uri? ParseLink(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        var link = values.FirstOrDefault();
        if (string.IsNullOrEmpty(link) || !link.StartsWith('<'))
        {
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"invalid next link {link}: missing '<");
        }

        if (link.IndexOf('>') is var index && index == -1)
        {
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"invalid next link {link}: missing '>'");
        }

        // Remove the first and last character
        link = link[1..index];

        if (!Uri.IsWellFormedUriString(link, UriKind.RelativeOrAbsolute))
        {
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"invalid next link {link}");
        }

        return new Uri(response.RequestMessage!.RequestUri!, link);
    }

    /// <summary>
    /// VerifyContentDigest verifies "Docker-Content-Digest" header if present.
    /// OCI distribution-spec states the Docker-Content-Digest header is optional.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#legacy-docker-support-http-headers
    /// </summary>
    /// <param name="response"></param>
    /// <param name="expected"></param>
    /// <exception cref="NotImplementedException"></exception>
    public static void VerifyContentDigest(this HttpResponseMessage response, string expected)
    {
        if (!response.Headers.TryGetValues(_dockerContentDigestHeader, out var digestHeader))
        {
            return;
        }
        var digestValue = digestHeader.FirstOrDefault();
        if (string.IsNullOrEmpty(digestValue))
        {
            return;
        }

        string digest;
        try
        {
            digest = Digest.Validate(digestValue);
        }
        catch (Exception)
        {
            throw new HttpIOException(
                HttpRequestError.InvalidResponse,
                $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: invalid response header: `Docker-Content-Digest: {digestValue}`");
        }
        if (digest != expected)
        {
            throw new HttpIOException(
                HttpRequestError.InvalidResponse,
                $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: invalid response; digest mismatch in Docker-Content-Digest: received {digest} when expecting {expected}");
        }
    }

    /// <summary>
    /// CheckOciSubjectHeader checks if the response header contains "OCI-Subject",
    /// repository ReferrerState is set to supported if it is present
    /// </summary>
    /// <param name="response"></param>
    /// <param name="repository"></param>
    internal static void CheckOciSubjectHeader(this HttpResponseMessage response, Repository repository)
    {
        if (repository.ReferrersState == Referrers.ReferrersState.Unknown && response.Headers.Contains("OCI-Subject"))
        {
            // Set it to Supported when the response header contains OCI-Subject
            repository.SetReferrersState(true);
        }

        // If the "OCI-Subject" header is NOT set, it means that either the manifest
        // has no subject OR the referrers API is NOT supported by the registry.
        // Since we don't know whether the pushed manifest has a subject or not,
        // we do not set the ReferrerState to NotSupported here.
    }

    /// <summary>
    /// Returns a descriptor generated from the response.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="expectedDigest"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Descriptor GenerateBlobDescriptor(this HttpResponseMessage response, string expectedDigest)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(mediaType))
        {
            mediaType = MediaTypeNames.Application.Octet;
        }
        var size = response.Content.Headers.ContentLength ??
            throw new HttpIOException(
                HttpRequestError.InvalidResponse,
                $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: unknown response Content-Length");
        response.VerifyContentDigest(expectedDigest);
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = expectedDigest,
            Size = size
        };
    }

    /// <summary>
    /// Returns a descriptor generated from the response.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="reference"></param>
    /// <param name="httpMethod"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> GenerateDescriptorAsync(this HttpResponseMessage response, Reference reference, CancellationToken cancellationToken)
    {
        // 1. Validate Content-Type
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!MediaTypeHeaderValue.TryParse(mediaType, out _))
        {
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: invalid response `Content-Type` header");
        }

        // 2. Validate Size
        var size = response.Content.Headers.ContentLength ?? throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: unknown response Content-Length");

        // 3. Validate Client Reference
        string? refDigest = null;
        try
        {
            refDigest = reference.Digest;
        }
        catch { }

        // 4. Validate Server Digest (if present)
        string? serverDigest = null;
        if (response.Headers.TryGetValues(_dockerContentDigestHeader, out var serverHeaderDigest))
        {
            serverDigest = serverHeaderDigest.FirstOrDefault();
            if (!string.IsNullOrEmpty(serverDigest))
            {
                response.VerifyContentDigest(serverDigest);
            }
        }

        // 5. Now, look for specific error conditions;
        string contentDigest;
        if (string.IsNullOrEmpty(serverDigest))
        {
            if (response.RequestMessage!.Method == HttpMethod.Head)
            {
                if (string.IsNullOrEmpty(refDigest))
                {
                    // HEAD without server `Docker-Content-Digest`
                    // immediate fail
                    throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage.Method} {response.RequestMessage.RequestUri}: missing required header {serverHeaderDigest}");
                }
                // Otherwise, just trust the client-supplied digest
                contentDigest = refDigest;
            }
            else
            {
                // GET without server `Docker-Content-Digest header forces the
                // expensive calculation
                try
                {
                    contentDigest = await response.CalculateDigestFromResponse(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    throw new HttpIOException(HttpRequestError.InvalidResponse, $"failed to calculate digest on response body; {e.Message}");
                }
            }
        }
        else
        {
            contentDigest = serverDigest;
        }
        if (!string.IsNullOrEmpty(refDigest) && refDigest != contentDigest)
        {
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: invalid response; digest mismatch in {serverHeaderDigest}: received {contentDigest} when expecting {refDigest}");
        }

        // 6. Finally, if we made it this far, then all is good; return the descriptor
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = contentDigest,
            Size = size
        };
    }

    /// <summary>
    /// CalculateDigestFromResponse calculates the actual digest of the response body
    /// taking care not to destroy it in the process
    /// </summary>
    /// <param name="response"></param>
    private static async Task<string> CalculateDigestFromResponse(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return Digest.ComputeSha256(bytes);
    }
}
