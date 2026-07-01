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
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// The default <see cref="IAuthChallengeHandler"/>: implements the standard OCI distribution
/// authentication flow. It parses the <c>WWW-Authenticate</c> challenge, validates the realm,
/// and fetches a Basic or Bearer token. An unrecognized (or absent) challenge yields
/// <c>null</c>, mirroring the client's prior behavior of returning the original 401 unchanged.
/// </summary>
/// <remarks>
/// This handler issues no extra requests beyond the standard token fetch — compliant
/// registries are unaffected. It does not attempt to recover from non-conformant registries;
/// supply a custom <see cref="IAuthChallengeHandler"/> for that.
/// </remarks>
public sealed class DefaultAuthChallengeHandler : IAuthChallengeHandler
{
    /// <inheritdoc />
    public async Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
        AuthChallengeContext context,
        CancellationToken cancellationToken = default)
    {
        switch (context.Scheme)
        {
            case Challenge.Scheme.Basic:
                {
                    var basicToken = await context
                        .FetchBasicTokenAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return new AuthChallengeResolution
                    {
                        Scheme = Challenge.Scheme.Basic,
                        Token = basicToken,
                        CacheScopeKey = string.Empty,
                    };
                }
            case Challenge.Scheme.Bearer:
                return await ResolveBearerAsync(context, context.ChallengeParameters, cancellationToken)
                    .ConfigureAwait(false);
            default:
                return null;
        }
    }

    private static async Task<AuthChallengeResolution> ResolveBearerAsync(
        AuthChallengeContext context,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            throw new AuthenticationException(
                "Missing parameters in the Www-Authenticate challenge.");
        }

        var (scopes, cacheKey) = context.MergeChallengeScopes(
            parameters.TryGetValue("scope", out var scopeValue) ? scopeValue : null);

        if (!parameters.TryGetValue("realm", out var realm))
        {
            // 'realm' is required as it specifies the token endpoint URL for the challenge.
            throw new KeyNotFoundException(
                "Missing 'realm' parameter in WWW-Authenticate Bearer challenge.");
        }

        // Validate realm URL before sending credentials.
        if (!Uri.TryCreate(realm, UriKind.Absolute, out var realmUri))
        {
            throw new AuthenticationException($"Invalid realm URL: '{realm}'");
        }

        if (!await context.IsRealmAllowedAsync(realmUri, cancellationToken).ConfigureAwait(false))
        {
            throw new AuthenticationException(
                $"Authentication realm '{realmUri}' is not allowed for registry '{context.OriginalRequest.RequestUri!.Authority}'.");
        }

        if (!parameters.TryGetValue("service", out var service))
        {
            // some registries may omit the `service` parameter; use an empty string when absent.
            service = string.Empty;
        }

        var bearerToken = await context
            .FetchBearerTokenAsync(realm, service, scopes, cancellationToken)
            .ConfigureAwait(false);

        return new AuthChallengeResolution
        {
            Scheme = Challenge.Scheme.Bearer,
            Token = bearerToken,
            CacheScopeKey = cacheKey,
        };
    }
}
