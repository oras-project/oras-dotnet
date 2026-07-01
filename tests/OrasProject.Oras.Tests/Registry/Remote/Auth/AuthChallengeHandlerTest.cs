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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Auth;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

/// <summary>
/// Tests for the pluggable <see cref="IAuthChallengeHandler"/> seam. These demonstrate that a
/// custom handler can recover from a non-conformant registry whose token-carrying 401 is
/// unusable (no <c>WWW-Authenticate</c> challenge) by issuing a credential-free probe — the
/// scenario the default handler intentionally does not handle.
/// </summary>
public class AuthChallengeHandlerTest
{
    private const string _host = "dhi.example.com";
    private const string _staleToken = "stale_token";
    private const string _freshToken = "fresh_token";

    /// <summary>
    /// Simulates a registry (e.g. dhi.io) that returns a 401 with NO challenge when a stale
    /// bearer token is presented, but returns a usable Bearer challenge on a cold (no-auth)
    /// request, and issues a fresh token from its token endpoint.
    /// </summary>
    private static HttpResponseMessage NonConformantRegistry(HttpRequestMessage req, CancellationToken cancellationToken)
    {
        // Token endpoint: hand out a fresh token (anonymous fetch).
        if (req.RequestUri!.AbsolutePath == "/token")
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"access_token\":\"{_freshToken}\"}}"),
                RequestMessage = req,
            };
        }

        var token = req.Headers.Authorization?.Parameter;

        // Fresh token is accepted.
        if (token == _freshToken)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req };
        }

        // Stale token is rejected WITHOUT a challenge (the non-conformant behavior).
        if (token == _staleToken)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
        }

        // Cold (no-auth) request yields a usable, same-host Bearer challenge.
        var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
        unauthorized.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue("Bearer", $"realm=\"https://{_host}/token\",service=\"{_host}\""));
        return unauthorized;
    }

    [Fact]
    public async Task CustomHandler_RecoversFromMissingChallenge_WhenCachedTokenIsStale()
    {
        // Arrange: a client whose cache holds a stale token, using a custom handler that
        // cold-probes to recover when the token-carrying 401 has no usable challenge.
        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object))
        {
            AuthChallengeHandler = new ColdReDeriveHandler(),
        };
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_host}/v2/");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the cold-probe recovery produced a fresh token and the retry succeeded.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DefaultHandler_DoesNotRecoverFromMissingChallenge_ReturnsUnauthorized()
    {
        // Arrange: identical setup, but with the default handler (no custom recovery).
        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object));
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_host}/v2/");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the unusable 401 is surfaced unchanged — the seam is what enables recovery.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CustomHandler_ReturningNull_ReturnsOriginalUnauthorized()
    {
        // Arrange: a handler that always gives up.
        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object))
        {
            AuthChallengeHandler = new GiveUpHandler(),
        };
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_host}/v2/");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendWithoutAuthorizationAsync_ThrowsForNonReplayableRequest()
    {
        // Arrange: a POST is not idempotent, so the cold-probe primitive must refuse to replay it.
        InvalidOperationException? captured = null;
        var handler = new DelegateHandler(async (context, ct) =>
        {
            Assert.False(context.CanReplayOriginalRequest);
            captured = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SendWithoutAuthorizationAsync(ct));
            return null;
        });

        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object))
        {
            AuthChallengeHandler = handler,
        };
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{_host}/v2/")
        {
            Content = new StringContent("payload"),
        };

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidResolution_UnsupportedScheme_Throws()
    {
        // A handler must return Basic or Bearer; Unknown must be rejected, not silently sent.
        var handler = new DelegateHandler((context, ct) => Task.FromResult<AuthChallengeResolution?>(
            new AuthChallengeResolution { Scheme = Challenge.Scheme.Unknown, Token = "x" }));
        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object))
        {
            AuthChallengeHandler = handler,
        };
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_host}/v2/");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task InvalidResolution_EmptyToken_Throws()
    {
        // A handler must return a non-empty token.
        var handler = new DelegateHandler((context, ct) => Task.FromResult<AuthChallengeResolution?>(
            new AuthChallengeResolution { Scheme = Challenge.Scheme.Bearer, Token = string.Empty }));
        var client = new Client(new HttpClient(CustomHandler(NonConformantRegistry).Object))
        {
            AuthChallengeHandler = handler,
        };
        client.Cache.SetCache(_host, Challenge.Scheme.Bearer, string.Empty, _staleToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_host}/v2/");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// A handler that, on an unusable (missing/unknown) challenge with a stale cached token,
    /// re-derives a usable challenge from a credential-free probe and acquires a fresh token.
    /// This mirrors the recovery a pull-through-cache consumer would implement.
    /// </summary>
    private sealed class ColdReDeriveHandler : IAuthChallengeHandler
    {
        private static readonly DefaultAuthChallengeHandler _standard = new();

        public async Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
            AuthChallengeContext context,
            CancellationToken cancellationToken = default)
        {
            var standardResolution = await _standard
                .ResolveAuthorizationAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (standardResolution != null)
            {
                return standardResolution;
            }

            // The challenge was unusable. Only recover when a cached token was attached (so a
            // genuine cold bad-credential failure is not masked) and the request is replayable.
            if (!context.AttachedCachedToken || !context.CanReplayOriginalRequest)
            {
                return null;
            }

            using var cold = await context
                .SendWithoutAuthorizationAsync(cancellationToken)
                .ConfigureAwait(false);
            if (cold.StatusCode != HttpStatusCode.Unauthorized)
            {
                return null;
            }

            var (scheme, parameters) = Challenge.ParseChallenge(
                cold.Headers.WwwAuthenticate.FirstOrDefault()?.ToString());
            if (scheme != Challenge.Scheme.Bearer || parameters == null ||
                !parameters.TryGetValue("realm", out var realm) ||
                !Uri.TryCreate(realm, UriKind.Absolute, out var realmUri))
            {
                return null;
            }

            if (!await context.IsRealmAllowedAsync(realmUri, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var (scopes, cacheKey) = context.MergeChallengeScopes(
                parameters.GetValueOrDefault("scope"));
            parameters.TryGetValue("service", out var service);
            var token = await context
                .FetchBearerTokenAsync(realm, service ?? string.Empty, scopes, cancellationToken)
                .ConfigureAwait(false);

            return new AuthChallengeResolution
            {
                Scheme = Challenge.Scheme.Bearer,
                Token = token,
                CacheScopeKey = cacheKey,
            };
        }
    }

    private sealed class GiveUpHandler : IAuthChallengeHandler
    {
        public Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
            AuthChallengeContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AuthChallengeResolution?>(null);
    }

    private sealed class DelegateHandler : IAuthChallengeHandler
    {
        private readonly Func<AuthChallengeContext, CancellationToken, Task<AuthChallengeResolution?>> _func;

        public DelegateHandler(Func<AuthChallengeContext, CancellationToken, Task<AuthChallengeResolution?>> func)
            => _func = func;

        public Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
            AuthChallengeContext context,
            CancellationToken cancellationToken = default)
            => _func(context, cancellationToken);
    }
}
