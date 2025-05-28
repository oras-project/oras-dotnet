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
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal static class HttpRequestMessageExtensions
{
    private const string _userAgent = "oras-dotnet";

    /// <summary>
    /// CloneAsync creates a deep copy of the specified <see cref="HttpRequestMessage"/> instance, including its content, headers, and options.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to clone.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cloned <see cref="HttpRequestMessage"/>.</returns>
    internal static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content != null
                ? await request.Content.CloneAsync().ConfigureAwait(false)
                : null,
            Version = request.Version
        };
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
    /// CloneAsync creates a deep copy of the specified <see cref="HttpContent"/> instance, including its headers.
    /// </summary>
    /// <param name="content">The <see cref="HttpContent"/> to clone.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cloned <see cref="HttpContent"/>.</returns>
    internal static async Task<HttpContent> CloneAsync(this HttpContent content)
    {
        var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        HttpContent clone;
        if (stream.CanSeek)
        {
            // If the stream supports seeking, we can rewind and reuse it
            stream.Position = 0;
            clone = new StreamContent(stream);
        }
        else
        {
            var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream).ConfigureAwait(false);
            memoryStream.Position = 0;
            clone = new StreamContent(memoryStream);
        }

        foreach (var header in content.Headers)
        {
            clone.Headers.Add(header.Key, header.Value);
        }
        return clone;
    }

    /// <summary>
    /// AddDefaultUserAgent adds the default user agent oras-dotnet
    /// </summary>
    /// <param name="requestMessage"></param>
    /// <returns></returns>
    public static HttpRequestMessage AddDefaultUserAgent(this HttpRequestMessage requestMessage)
    {
        if (requestMessage.Headers.UserAgent.Count == 0)
        {
            requestMessage.Headers.UserAgent.ParseAdd(_userAgent);
        }
        return requestMessage;
    }
}
