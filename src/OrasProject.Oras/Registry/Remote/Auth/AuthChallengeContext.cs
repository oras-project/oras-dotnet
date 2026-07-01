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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Per-request context supplied to an <see cref="IAuthChallengeHandler"/> when a request
/// receives an HTTP 401 response. Exposes the unauthorized exchange plus capabilities —
/// token fetch, realm validation, scope merging, cached-token lookup, and a credential-free
/// probe — that delegate to the owning <see cref="Client"/>.
/// </summary>
/// <remarks>
/// <para>
/// The context is valid only for the duration of the
/// <see cref="IAuthChallengeHandler.ResolveAuthorizationAsync"/> call and must not be
/// retained. <see cref="OriginalRequest"/> and <see cref="UnauthorizedResponse"/> are owned
/// by the client and must not be disposed by the handler. Any response returned from
/// <see cref="SendWithoutAuthorizationAsync"/> is owned by the caller and must be disposed.
/// </para>
/// <para>
/// <b>Security:</b> a custom handler that fetches a token via
/// <see cref="FetchBearerTokenAsync"/> is responsible for validating the realm first (see
/// <see cref="IsRealmAllowedAsync"/>). The default handler always validates the realm.
/// </para>
/// </remarks>
public sealed class AuthChallengeContext
{
    private readonly Client _client;
    private readonly bool _allowAutoRedirect;

    internal AuthChallengeContext(
        Client client,
        HttpRequestMessage originalRequest,
        HttpResponseMessage unauthorizedResponse,
        string host,
        string? partitionId,
        bool attachedCachedToken,
        IReadOnlyList<string> requestedScopes,
        bool allowAutoRedirect,
        Challenge.Scheme scheme,
        IReadOnlyDictionary<string, string>? challengeParameters)
    {
        _client = client;
        _allowAutoRedirect = allowAutoRedirect;
        OriginalRequest = originalRequest;
        UnauthorizedResponse = unauthorizedResponse;
        Host = host;
        PartitionId = partitionId;
        AttachedCachedToken = attachedCachedToken;
        RequestedScopes = requestedScopes;
        Scheme = scheme;
        ChallengeParameters = challengeParameters;
        CanReplayOriginalRequest =
            originalRequest.Method == HttpMethod.Get ||
            originalRequest.Method == HttpMethod.Head;
    }

    /// <summary>
    /// The original request that received the 401. Its <c>Authorization</c> header is unset.
    /// </summary>
    public HttpRequestMessage OriginalRequest { get; }

    /// <summary>
    /// The 401 (Unauthorized) response, including its <c>WWW-Authenticate</c> challenge.
    /// </summary>
    public HttpResponseMessage UnauthorizedResponse { get; }

    /// <summary>
    /// The scheme of the original challenge, parsed from <see cref="UnauthorizedResponse"/>.
    /// <see cref="Challenge.Scheme.Unknown"/> when there is no usable challenge (e.g. a
    /// non-conformant registry that omits the header on a token-carrying 401).
    /// </summary>
    public Challenge.Scheme Scheme { get; }

    /// <summary>
    /// The parameters of the original Bearer challenge (e.g. <c>realm</c>, <c>service</c>,
    /// <c>scope</c>), or <c>null</c> for a non-Bearer or absent challenge. This is the parsed
    /// form of <see cref="UnauthorizedResponse"/>'s challenge; a handler that probes for a
    /// fresh challenge must parse that probe response itself.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ChallengeParameters { get; }

    /// <summary>
    /// The registry authority (host, with port when non-default) that issued the challenge.
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// The cache partition identifier for the request, if any.
    /// </summary>
    public string? PartitionId { get; }

    /// <summary>
    /// <c>true</c> when the failed attempt carried a cached token — i.e., this 401 may be a
    /// stale-token rejection rather than a first-time challenge. Useful for gating recovery so
    /// that genuine bad-credential failures on a cold request are not masked.
    /// </summary>
    public bool AttachedCachedToken { get; }

    /// <summary>
    /// The scopes the client computed for <see cref="Host"/> prior to this challenge.
    /// </summary>
    public IReadOnlyList<string> RequestedScopes { get; }

    /// <summary>
    /// <c>true</c> when <see cref="OriginalRequest"/> can be safely re-sent without
    /// authorization — that is, an idempotent <c>GET</c> or <c>HEAD</c>. Gates
    /// <see cref="SendWithoutAuthorizationAsync"/> so that non-idempotent uploads are never
    /// replayed.
    /// </summary>
    public bool CanReplayOriginalRequest { get; }

    /// <summary>
    /// Re-sends <see cref="OriginalRequest"/> with no <c>Authorization</c> header to elicit a
    /// fresh <c>WWW-Authenticate</c> challenge. This is the recovery primitive for registries
    /// whose token-carrying 401 is unusable. The returned response is owned by the caller and
    /// must be disposed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The response to the credential-free request.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CanReplayOriginalRequest"/> is <c>false</c>.
    /// </exception>
    public async Task<HttpResponseMessage> SendWithoutAuthorizationAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanReplayOriginalRequest)
        {
            throw new InvalidOperationException(
                "The original request cannot be safely replayed without authorization " +
                "because it is not an idempotent GET or HEAD request.");
        }

        var coldRequest = await OriginalRequest
            .CloneAsync(rewindContent: true, cancellationToken)
            .ConfigureAwait(false);
        coldRequest.Headers.Authorization = null;
        return await _client
            .SendRequestAsync(coldRequest, _allowAutoRedirect, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Validates a realm URL against the original request URI using the client's
    /// <see cref="Client.RealmValidator"/>.
    /// </summary>
    /// <param name="realmUri">The realm URL to validate.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the realm is allowed for the original request; otherwise <c>false</c>.</returns>
    public Task<bool> IsRealmAllowedAsync(
        Uri realmUri,
        CancellationToken cancellationToken = default)
        => _client.RealmValidator.IsRealmAllowedAsync(
            OriginalRequest.RequestUri!, realmUri, cancellationToken);

    /// <summary>
    /// Fetches a bearer token from the given realm, reusing the client's access-token and
    /// credential providers. Anonymous-token behavior is preserved when no credentials exist.
    /// </summary>
    /// <param name="realm">The token endpoint URL from the challenge.</param>
    /// <param name="service">The service identifier from the challenge.</param>
    /// <param name="scopes">The scopes to request for the token.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The fetched bearer token.</returns>
    public Task<string> FetchBearerTokenAsync(
        string realm,
        string service,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
        => _client.FetchBearerAuthAsync(
            Host,
            realm,
            service,
            scopes as IList<string> ?? scopes.ToList(),
            forceRefresh: true,
            cancellationToken);

    /// <summary>
    /// Fetches a Basic authentication token (base64 <c>username:password</c>) for
    /// <see cref="Host"/> from the client's credential provider.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The base64-encoded Basic credential.</returns>
    public Task<string> FetchBasicTokenAsync(CancellationToken cancellationToken = default)
        => _client.FetchBasicAuthAsync(Host, cancellationToken);

    /// <summary>
    /// Attempts to read a still-cached token for <see cref="Host"/> at the given scheme and
    /// scope key.
    /// </summary>
    /// <param name="scheme">The authentication scheme to look up.</param>
    /// <param name="scopeKey">The scope key to look up.</param>
    /// <param name="token">When this method returns, the cached token, or an empty string.</param>
    /// <returns><c>true</c> if a matching token was found; otherwise <c>false</c>.</returns>
    public bool TryGetCachedToken(Challenge.Scheme scheme, string scopeKey, out string token)
        => _client.Cache.TryGetToken(Host, scheme, scopeKey, out token, PartitionId);

    /// <summary>
    /// Merges the challenge's <c>scope</c> parameter with the existing scopes for
    /// <see cref="Host"/>, returning the token-request scope list and the cache key
    /// (the space-joined merged scope set).
    /// </summary>
    /// <param name="scopeParameter">
    /// The raw <c>scope</c> parameter value from the challenge, or <c>null</c> when absent.
    /// </param>
    /// <returns>The merged scopes for the token request and the cache key to store it under.</returns>
    public (IReadOnlyList<string> Scopes, string CacheKey) MergeChallengeScopes(string? scopeParameter)
    {
        var newScopes = new SortedSet<Scope>(_client.ScopeManager.GetScopesForHost(Host));
        if (!string.IsNullOrEmpty(scopeParameter))
        {
            foreach (var scopeStr in scopeParameter.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Scope.TryParse(scopeStr, out var scope))
                {
                    Scope.AddOrMergeScope(newScopes, scope);
                }
            }
        }

        var cacheKey = string.Join(" ", newScopes);
        var scopes = newScopes.Select(scope => scope.ToString()).ToList();
        return (scopes, cacheKey);
    }
}
