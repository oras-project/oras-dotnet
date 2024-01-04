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

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

internal static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Parses the error returned by the remote registry.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static async Task<Exception> ParseErrorResponseAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new Exception(new
        {
            response.RequestMessage!.Method,
            URL = response.RequestMessage.RequestUri,
            response.StatusCode,
            Errors = body
        }.ToString());
    }

    /// <summary>
    /// Returns the URL of the response's "Link" header, if present.
    /// </summary>
    /// <returns>next link or null if not present</returns>
    public static Uri? ParseLink(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        var link = values.FirstOrDefault();
        if (string.IsNullOrEmpty(link) || link[0] != '<')
        {
            throw new Exception($"invalid next link {link}: missing '<");
        }
        if (link.IndexOf('>') is var index && index == -1)
        {
            throw new Exception($"invalid next link {link}: missing '>'");
        }
        link = link[1..index];
        if (!Uri.IsWellFormedUriString(link, UriKind.RelativeOrAbsolute))
        {
            throw new Exception($"invalid next link {link}");
        }

        return new Uri(response.RequestMessage!.RequestUri!, link);
    }
}
