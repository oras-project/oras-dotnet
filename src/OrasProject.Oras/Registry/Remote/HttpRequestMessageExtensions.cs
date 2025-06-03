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

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal static class HttpRequestMessageExtensions
{
    private const string _userAgent = "oras-dotnet";

    /// <summary>
    /// CloneAsync creates a deep copy of the specified <see cref="HttpRequestMessage"/> instance, including its content, headers, and options.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to clone.</param>
    /// <param name="cancellationToken">A token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cloned <see cref="HttpRequestMessage"/>.</returns>
    internal static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request, bool rewindContent = true, CancellationToken cancellationToken = default)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };
        if (rewindContent)
        {
            clone.Content = await request.Content.RewindAndCloneAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            clone.Content = request.Content; // reuse the original content without cloning
        }
        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    /// <summary>
    /// Creates a new HttpContent instance by rewinding the stream of the original content.
    /// We avoid doing a deep copy of the content as it can be very expensive for large payloads.
    /// </summary>
    /// <param name="content">The original <see cref="HttpContent"/> to rewind.</param>
    /// <param name="cancellationToken">A token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the new <see cref="HttpContent"/> with the same data as the original.</returns>
    /// <exception cref="IOException">Thrown when the source stream cannot be rewound.</exception>
    internal static async Task<HttpContent?> RewindAndCloneAsync(this HttpContent? content, CancellationToken cancellationToken = default)
    {
        if (content == null)
        {
            return null;
        }

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (!stream.CanSeek)
        {
            throw new IOException("The content stream is non-seekable and cannot be rewound.");
        }

        stream.Position = 0; // rewind the stream to the beginning
        var clone = new StreamContent(stream);
        foreach (var header in content.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }

    /// <summary>
    /// AddDefaultUserAgent adds the default user agent oras-dotnet
    /// </summary>
    /// <param name="requestMessage">The <see cref="HttpRequestMessage"/> to add the default user agent to.</param>
    /// <returns>The same <see cref="HttpRequestMessage"/> instance with the default user agent added (if needed).</returns>
    public static HttpRequestMessage AddDefaultUserAgent(this HttpRequestMessage requestMessage)
    {
        if (requestMessage.Headers.UserAgent.Count == 0)
        {
            requestMessage.Headers.UserAgent.ParseAdd(_userAgent);
        }
        return requestMessage;
    }
}
