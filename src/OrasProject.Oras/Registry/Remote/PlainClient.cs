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
/// PlainClient, which implements IClient, provides a way to access default HttpClients.
/// </summary>
public class PlainClient : IClient
{
    private readonly HttpClient _client;
    private readonly HttpClient _noRedirectClient;

    /// <summary>
    /// Initializes a new instance of the PlainClient class.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow redirects.
    /// If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    public PlainClient(HttpClient? httpClient = null)
        : this(httpClient, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the PlainClient class with separate HttpClient instances for redirect control.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow redirects.
    /// If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with <c>AllowAutoRedirect = false</c> for capturing redirect locations.
    /// If not provided, uses <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// <para>
    /// <strong>Advanced Usage:</strong> To apply consistent HTTP configuration (timeouts, proxy, headers) 
    /// across both redirect and no-redirect scenarios, provide both <paramref name="httpClient"/> and 
    /// <paramref name="noRedirectHttpClient"/> with the same base configuration but different redirect settings.
    /// This is useful with IHttpClientFactory or custom HttpClient management.
    /// </para>
    /// </param>
    public PlainClient(HttpClient? httpClient, HttpClient? noRedirectHttpClient)
    {
        _client = httpClient ?? DefaultHttpClient.Instance;
        _noRedirectClient = noRedirectHttpClient ?? DefaultHttpClient.NoRedirectInstance;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage originalRequest, CancellationToken cancellationToken = default)
    {
        originalRequest.AddDefaultUserAgent();
        return await _client.SendAsync(originalRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an HTTP request with optional redirect control.
    /// </summary>
    /// <param name="originalRequest">The HTTP request message to send.</param>
    /// <param name="allowAutoRedirect">
    /// Whether to follow redirects automatically. When <c>false</c>, uses the configured no-redirect
    /// <see cref="HttpClient"/> instance (see constructors) to capture redirect locations without following
    /// them (e.g., for <c>GetBlobLocationAsync</c>). By default, this is <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message.</returns>
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
