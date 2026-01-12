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

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// PlainClient, which implements IClient, provides a way to access default HttpClient
/// </summary>
/// <param name="client"></param>
public class PlainClient(HttpClient? httpClient = null) : IClient
{
    private readonly HttpClient _client = httpClient ?? DefaultHttpClient.Instance;

    /// <summary>
    /// HttpClient configured to not follow redirects.
    /// Used for scenarios where we need to capture redirect locations (e.g., blob location URLs).
    /// </summary>
    private readonly HttpClient _noRedirectClient = DefaultHttpClient.NoRedirectInstance;

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage originalRequest, CancellationToken cancellationToken = default)
    {
        originalRequest.AddDefaultUserAgent();
        return await _client.SendAsync(originalRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage originalRequest,
        bool allowAutoRedirect,
        CancellationToken cancellationToken = default
    )
    {
        originalRequest.AddDefaultUserAgent();
        var client = allowAutoRedirect ? _client : _noRedirectClient;
        return await client.SendAsync(originalRequest, cancellationToken).ConfigureAwait(false);
    }
}
