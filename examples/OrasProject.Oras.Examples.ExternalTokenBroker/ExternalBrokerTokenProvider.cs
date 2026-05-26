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
#region Usage
using System.Net.Http.Json;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Examples.ExternalTokenBroker;

/// <summary>
/// An <see cref="IAccessTokenProvider"/> that delegates token acquisition
/// to an external broker service via HTTP.
/// </summary>
/// <remarks>
/// <para>
/// This pattern is useful when long-lived credentials (passwords, refresh
/// tokens) should not enter the ORAS process.  The broker performs the
/// credential exchange externally and returns only a short-lived,
/// narrowly-scoped access token.
/// </para>
/// <para>
/// The provider is only called when the <see cref="Client"/>'s token cache
/// does not contain a valid token for the requested scopes (i.e., on the
/// first request or after the server rejects a cached token).
/// </para>
/// </remarks>
public class ExternalBrokerTokenProvider : IAccessTokenProvider
{
    private readonly HttpClient _brokerClient;
    private readonly string _brokerEndpoint;

    /// <summary>
    /// Initializes a new instance with the broker HTTP client and endpoint.
    /// </summary>
    /// <param name="brokerClient">
    /// An <see cref="HttpClient"/> configured to communicate with the
    /// token broker (base address, auth headers, etc.).
    /// </param>
    /// <param name="brokerEndpoint">
    /// The broker endpoint path (e.g., "/api/v1/token").
    /// </param>
    public ExternalBrokerTokenProvider(
        HttpClient brokerClient, string brokerEndpoint)
    {
        _brokerClient = brokerClient;
        _brokerEndpoint = brokerEndpoint;
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveAccessTokenAsync(
        string hostname,
        string realm,
        string service,
        IReadOnlyList<string> scopes,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // Forward the challenge parameters to the broker.
        // The broker owns the credential exchange and returns only a
        // short-lived token — no secrets cross the process boundary.
        //
        // IMPORTANT: realm, service, and scopes originate from the
        // server's WWW-Authenticate header and are untrusted. The
        // broker should validate these values (e.g., require HTTPS
        // realm, match an allowlist) before acting on them.
        var request = new TokenRequest(
            hostname, realm, service, scopes, forceRefresh);

        using var response = await _brokerClient.PostAsJsonAsync(
            _brokerEndpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);
        return result?.AccessToken;
    }

    private record TokenRequest(
        string Hostname,
        string Realm,
        string Service,
        IReadOnlyList<string> Scopes,
        bool ForceRefresh);

    private record TokenResponse(string? AccessToken);
}
#endregion
