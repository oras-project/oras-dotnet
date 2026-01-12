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
/// PlainClient, which implements IClient, provides a way to access default HttpClient.
/// </summary>
/// <param name="httpClient">
/// Optional HttpClient to use for requests. If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
/// <para>
/// <strong>Important:</strong> When using the <see cref="SendAsync(HttpRequestMessage, bool, CancellationToken)"/> 
/// overload with <c>allowAutoRedirect = false</c>, a separate singleton HttpClient 
/// (<see cref="DefaultHttpClient.NoRedirectInstance"/>) configured with <c>AllowAutoRedirect = false</c> 
/// is always used, regardless of the <paramref name="httpClient"/> parameter. This ensures proper redirect 
/// control behavior for operations like <c>GetBlobLocationAsync</c>.
/// </para>
/// <para>
/// This means custom HttpClient configurations (timeouts, proxy settings, custom headers, etc.) 
/// will <strong>not</strong> apply to redirect-disabled requests.
/// </para>
/// </param>
public class PlainClient(HttpClient? httpClient = null) : IClient
{
    private readonly HttpClient _client = httpClient ?? DefaultHttpClient.Instance;

    /// <summary>
    /// HttpClient configured to not follow redirects.
    /// Used for scenarios where we need to capture redirect locations (e.g., blob location URLs).
    /// This is always the singleton <see cref="DefaultHttpClient.NoRedirectInstance"/>, 
    /// independent of any custom HttpClient provided to the constructor.
    /// </summary>
    private readonly HttpClient _noRedirectClient = DefaultHttpClient.NoRedirectInstance;

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
    /// Whether to follow redirects automatically. When <c>false</c>, uses the singleton 
    /// <see cref="DefaultHttpClient.NoRedirectInstance"/> to capture redirect locations 
    /// without following them (e.g., for <c>GetBlobLocationAsync</c>).
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
