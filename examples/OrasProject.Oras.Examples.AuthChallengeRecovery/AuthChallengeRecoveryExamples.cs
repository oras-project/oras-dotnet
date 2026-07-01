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
using System.Net;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Examples.AuthChallengeRecovery;

/// <summary>
/// Demonstrates supplying a custom <see cref="IAuthChallengeHandler"/> that recovers from
/// non-conformant registries whose token-carrying <c>401</c> response is unusable — for example
/// a registry that omits the <c>WWW-Authenticate</c> challenge when a stale token is presented,
/// or returns one whose realm points at a different host — by re-deriving a usable challenge
/// from a credential-free probe.
/// </summary>
public static class AuthChallengeRecoveryExamples
{
    /// <summary>
    /// Creates a <see cref="Client"/> that falls back to cold-probe recovery when a cached token
    /// is rejected with an unusable challenge. Compliant registries are unaffected.
    /// </summary>
    /// <param name="httpClient">The HTTP client the registry client should use.</param>
    /// <returns>A configured <see cref="Client"/>.</returns>
    public static Client CreateClientWithChallengeRecovery(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        return new Client(httpClient)
        {
            AuthChallengeHandler = new ColdReDeriveAuthChallengeHandler(),
        };
    }
}

/// <summary>
/// An <see cref="IAuthChallengeHandler"/> that runs the standard OCI challenge flow and, when the
/// token-carrying <c>401</c>'s challenge is UNUSABLE (absent, or a missing / malformed / disallowed
/// realm) and a cached token was attached, re-derives a usable challenge from a credential-free
/// probe and acquires a fresh token.
/// </summary>
/// <remarks>
/// <para>
/// The handler is host-agnostic: it keys off the <em>shape</em> of the failure, not specific
/// registry names. Compliant registries incur no extra request, and a genuine credential failure
/// behind an allowed realm is surfaced rather than masked.
/// </para>
/// <para>
/// The recovery is gated on <see cref="AuthChallengeContext.AttachedCachedToken"/> (so a first-time
/// cold 401 is handled normally) and <see cref="AuthChallengeContext.CanReplayOriginalRequest"/>
/// (so non-idempotent requests are never replayed). The cold probe carries no token, so it cannot
/// re-enter this path — recovery is bounded to a single extra request.
/// </para>
/// </remarks>
public sealed class ColdReDeriveAuthChallengeHandler : IAuthChallengeHandler
{
    /// <inheritdoc />
    public async Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
        AuthChallengeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Try the original challenge. TryResolveAsync is non-throwing: an unusable challenge
        //    yields null, while a credential failure behind an allowed realm still throws.
        var resolution = await TryResolveAsync(
            context, context.Scheme, context.ChallengeParameters, cancellationToken);
        if (resolution != null)
        {
            return resolution;
        }

        // 2. Recover only when this looks like a stale-token rejection we can safely retry.
        if (!context.AttachedCachedToken || !context.CanReplayOriginalRequest)
        {
            return null;
        }

        // 3. Cold (no-auth) probe to elicit a usable challenge the upstream withheld.
        using var cold = await context.SendWithoutAuthorizationAsync(cancellationToken);
        if (cold.StatusCode != HttpStatusCode.Unauthorized)
        {
            return null;
        }

        Challenge.Scheme coldScheme;
        IReadOnlyDictionary<string, string>? coldParameters;
        try
        {
            (coldScheme, coldParameters) = Challenge.ParseChallenge(
                cold.Headers.WwwAuthenticate.FirstOrDefault()?.ToString());
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return null;
        }

        // 4. Resolve from the cold challenge (give up if it is also unusable).
        return await TryResolveAsync(context, coldScheme, coldParameters, cancellationToken);
    }

    /// <summary>
    /// Standard OCI resolution that returns <c>null</c> for an unusable challenge (no/unknown
    /// scheme, or a missing / malformed / disallowed realm) instead of throwing, so the caller can
    /// choose to recover. A token-endpoint failure behind an ALLOWED realm still throws, preserving
    /// genuine credential errors.
    /// </summary>
    private static async Task<AuthChallengeResolution?> TryResolveAsync(
        AuthChallengeContext context,
        Challenge.Scheme scheme,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        switch (scheme)
        {
            case Challenge.Scheme.Basic:
                return new AuthChallengeResolution
                {
                    Scheme = Challenge.Scheme.Basic,
                    Token = await context.FetchBasicTokenAsync(cancellationToken),
                    CacheScopeKey = string.Empty,
                };

            case Challenge.Scheme.Bearer:
                if (parameters == null ||
                    !parameters.TryGetValue("realm", out var realm) ||
                    !Uri.TryCreate(realm, UriKind.Absolute, out var realmUri) ||
                    !await context.IsRealmAllowedAsync(realmUri, cancellationToken))
                {
                    return null; // unusable challenge -> recoverable
                }

                var (scopes, cacheKey) = context.MergeChallengeScopes(parameters.GetValueOrDefault("scope"));
                parameters.TryGetValue("service", out var service);
                return new AuthChallengeResolution
                {
                    Scheme = Challenge.Scheme.Bearer,
                    Token = await context.FetchBearerTokenAsync(
                        realm, service ?? string.Empty, scopes, cancellationToken),
                    CacheScopeKey = cacheKey,
                };

            default:
                return null; // no/unknown challenge -> recoverable
        }
    }
}
#endregion
