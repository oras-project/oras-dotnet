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
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal static class HttpRequestMessageExtensions
{
    private const string _userAgent = "oras-dotnet";
    private const int _memoryBufferSize = 256 * 1024; // 256 KB

    /// <summary>
    /// CloneAsync creates a deep copy of the specified <see cref="HttpRequestMessage"/> instance, including its content, headers, and options.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to clone.</param>
    /// <param name="cancellationToken">A token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cloned <see cref="HttpRequestMessage"/>.</returns>
    internal static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content != null
                ? await request.Content.CloneAsync(cancellationToken).ConfigureAwait(false)
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
    /// <param name="cancellationToken">A token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cloned <see cref="HttpContent"/>.</returns>
    internal static async Task<HttpContent> CloneAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        HttpContent clone;
        if (stream.CanSeek)
        {
            // If the stream supports seeking, we can rewind and reuse it
            stream.Position = 0;
            clone = new StreamContent(stream);
            content.CopyHeadersTo(clone);
            return clone;
        }

        // If the stream does not support seeking, we clone it through a pipe
        var pipe = new Pipe();
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var buffer = pipe.Writer.GetMemory(_memoryBufferSize);
                    int bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break; // End of stream
                    }
                    pipe.Writer.Advance(bytesRead);
                    var result = await pipe.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    if (result.IsCompleted)
                    {
                        break; // No more data to write
                    }
                }
            }
            finally
            {
                await pipe.Writer.CompleteAsync().ConfigureAwait(false);
            }
        }, cancellationToken);

        var pipeReader = pipe.Reader.AsStream();
        clone = new StreamContent(pipeReader);
        content.CopyHeadersTo(clone);
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

    /// <summary>
    /// Copies all HTTP headers from the source <see cref="HttpContent"/> to the target <see cref="HttpContent"/>.
    /// </summary>
    /// <param name="source">The source <see cref="HttpContent"/> whose headers will be copied.</param>
    /// <param name="target">The target <see cref="HttpContent"/> to which headers will be added.</param>
    private static void CopyHeadersTo(this HttpContent source, HttpContent target)
    {
        foreach (var header in source.Headers)
        {
            target.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
