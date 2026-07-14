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

using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Moq;
using Moq.Protected;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry.Remote.Exceptions;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

/// <summary>
/// Tests for the opt-in <see cref="Client.ChallengeRecovery"/> seam and the built-in
/// <see cref="ChallengeRecoveries.ColdProbe"/> handler. These cover non-conformant upstreams that
/// mishandle a stale cached token (a registry that omits the challenge, points its realm at a
/// different host, or 401s the token request when the challenge is followed), while proving the seam is
/// inert for conformant registries and never masks a genuine credential failure.
/// </summary>
public class ChallengeRecoveryTest
{
    private const string _staleToken = "stale_bearer_token";
    private const string _freshToken = "fresh_access_token";
    private const string _requestPath = "/v2/app/manifests/v1";
    private const string _scope = "repository:app:pull";

    // The default Client cache is a process-wide shared MemoryCache. Use a unique host per test so
    // cache entries never collide across tests running in parallel.
    private static string NewHost() => $"reg-{Guid.NewGuid():N}.example.com";

    private static HttpResponseMessage Ok(HttpRequestMessage req)
        => new(HttpStatusCode.OK) { RequestMessage = req };

    private static HttpResponseMessage TokenResponse(HttpRequestMessage req)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"access_token\":\"{_freshToken}\"}}"),
            RequestMessage = req
        };

    private static HttpResponseMessage Unauthorized(HttpRequestMessage req, string? realm)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
        if (realm != null)
        {
            response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", $"realm=\"{realm}\",service=\"registry\",scope=\"{_scope}\""));
        }
        return response;
    }

    private static bool IsTokenEndpoint(HttpRequestMessage req)
        => req.RequestUri!.AbsolutePath.EndsWith("/token");

    private static bool IsRegistryProbe(HttpRequestMessage req)
        => req.RequestUri!.AbsolutePath == _requestPath && req.Headers.Authorization == null;

    // Seeds a stale cached bearer token so attempt 1 attaches it (mirrors a real stale-token pull).
    private static void SeedStaleToken(Client client, string host)
        => client.Cache.SetCache(host, Challenge.Scheme.Bearer, string.Empty, _staleToken);

    // Verifies attempt 1 actually carried the stale token — otherwise the request would degrade to a
    // credential-free probe and quietly exercise the wrong code path.
    private static void VerifyStaleTokenSent(Mock<System.Net.Http.DelegatingHandler> mockHandler)
        => mockHandler.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.AbsolutePath == _requestPath &&
                req.Headers.Authorization != null &&
                req.Headers.Authorization.Parameter == _staleToken),
            ItExpr.IsAny<CancellationToken>());

    private static void VerifyFreshTokenSent(Mock<System.Net.Http.DelegatingHandler> mockHandler)
        => mockHandler.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.AbsolutePath == _requestPath &&
                req.Headers.Authorization != null &&
                req.Headers.Authorization.Parameter == _freshToken),
            ItExpr.IsAny<CancellationToken>());

    private static void VerifyColdProbe(Mock<System.Net.Http.DelegatingHandler> mockHandler, Times times)
        => mockHandler.Protected().Verify(
            "SendAsync", times,
            ItExpr.Is<HttpRequestMessage>(req => IsRegistryProbe(req)),
            ItExpr.IsAny<CancellationToken>());

    private static void VerifyTokenFetch(Mock<System.Net.Http.DelegatingHandler> mockHandler, Times times)
        => mockHandler.Protected().Verify(
            "SendAsync", times,
            ItExpr.Is<HttpRequestMessage>(req => IsTokenEndpoint(req)),
            ItExpr.IsAny<CancellationToken>());

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NoChallengeOnStaleToken_ColdProbeRecovers()
    {
        // Arrange: a stale cached token provokes a 401 with NO WWW-Authenticate
        // challenge, but a credential-free request to the same URL yields a usable Bearer challenge.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, realm: null); // stale token → no challenge
            return Unauthorized(req, coldRealm);                             // cold probe → usable challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: recovered to 200 via a cold probe + a freshly fetched token.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_NoChallengeOnStaleToken_WithoutRecovery_ReturnsOriginal401()
    {
        // Arrange: identical to the no-challenge case, but recovery is NOT configured (default).
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, $"https://{host}/token");
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object)); // ChallengeRecovery == null
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the original 401 is returned unchanged — no cold probe, no token fetch.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_DeniedRealmOnStaleToken_ColdProbeRecovers()
    {
        // Arrange: a stale cached token provokes a 401 whose realm points at a foreign
        // host (rejected by the realm validator), but a cold request yields a same-host (allowed) realm.
        var host = NewHost();
        var foreignRealm = "https://foreign-auth.example.com/token";
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req) && req.RequestUri!.Host == host) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, foreignRealm); // stale → foreign realm
            return Unauthorized(req, coldRealm);                              // cold → same-host realm
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: recovered to 200 via the cold, same-host realm.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_DeniedRealmOnStaleToken_WithoutRecovery_Throws()
    {
        // Arrange: identical to the foreign-realm case, but recovery is NOT configured (default).
        var host = NewHost();
        var foreignRealm = "https://foreign-auth.example.com/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req) && req.RequestUri!.Host == host) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, foreignRealm);
            return Unauthorized(req, $"https://{host}/token");
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object)); // ChallengeRecovery == null
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: the foreign realm is rejected exactly as before — no cold probe masks it.
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        Assert.Contains("not allowed", ex.Message);
        VerifyColdProbe(mockHandler, Times.Never());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.OK)]                               // anonymous success → returned
    [InlineData(HttpStatusCode.Found, HttpStatusCode.Found)]                         // 302 redirect → returned
    [InlineData(HttpStatusCode.TemporaryRedirect, HttpStatusCode.TemporaryRedirect)] // 307 (blob location) → returned
    [InlineData(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized)]              // 403 → give up (original 401)
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized)]               // 404 → give up (original 401)
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.Unauthorized)]    // 5xx → give up (original 401)
    public async Task SendAsync_ChallengeRecovery_ColdProbeStatus_DeterminesOutcome(
        HttpStatusCode coldProbeStatus, HttpStatusCode expectedStatus)
    {
        // Arrange: a stale token provokes a no-challenge 401; the credential-free cold probe comes back
        // with `coldProbeStatus`. A 2xx success or 3xx redirect is a genuine recovery and is returned
        // as-is (e.g. an anonymous 200, or a blob-location 307 for an allowAutoRedirect:false caller);
        // any other status is discarded so the original 401 is surfaced rather than masked. The probe
        // never yields a fresh challenge, so no token is fetched.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            if (req.Headers.Authorization?.Parameter == _staleToken) return Unauthorized(req, realm: null);
            return new HttpResponseMessage(coldProbeStatus) { RequestMessage = req }; // cold probe (no auth)
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TokenEndpoint401OnStaleToken_ColdProbeRecovers()
    {
        // Arrange: nvcr-style. A stale cached token yields a challenge whose realm is a foreign host the
        // caller has explicitly trusted; following it to that token endpoint returns 401 (the registry
        // cannot mint a token for the stale-token-derived challenge). A credential-free cold probe yields
        // a usable same-host challenge whose token endpoint works.
        var host = NewHost();
        var foreignAuthHost = "authn.foreign.example.com";
        var foreignRealm = $"https://{foreignAuthHost}/token"; // trusted, but 401s the token request
        var coldRealm = $"https://{host}/token";               // same-host, mints a token

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req))
            {
                return req.RequestUri!.Host == foreignAuthHost
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req }
                    : TokenResponse(req);
            }
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, foreignRealm); // stale → foreign token endpoint
            return Unauthorized(req, coldRealm);                              // cold probe → same-host endpoint
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe,
            RealmValidator = new DefaultRealmValidator
            {
                TrustedRealmHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { foreignAuthHost }
            }
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: recovered to 200 via a single cold probe + a token from the working same-host endpoint.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TokenEndpoint401_ColdProbeAlsoFails_GivesUpAndRethrows()
    {
        // Arrange: a stale token yields a usable (allowed) challenge, but following it to the token
        // endpoint returns 401. Recovery is offered this failure and cold-probes once; here the cold
        // probe's challenge dead-ends at the same 401 token endpoint, so recovery gives up and rethrows
        // the original token-endpoint exception — attempted exactly once, but never masked.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return Unauthorized(req, realm: null);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge for both stale attempt and cold probe
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: the original token-endpoint 401 surfaces; recovery cold-probed exactly once (bounded).
        await Assert.ThrowsAsync<ResponseException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TokenEndpoint401_NoCachedToken_DoesNotProbe()
    {
        // Arrange: a genuine first-time failure — no cached token, so attempt 1 is credential-free. The
        // challenge is usable but the token endpoint returns 401 (e.g. bad credentials). ColdProbe
        // self-gates on AttachedCachedToken, which is false here, so recovery declines WITHOUT any cold
        // probe or re-derivation and the failure surfaces unchanged.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return Unauthorized(req, realm: null);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge on the credential-free request
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        // No SeedStaleToken: attempt 1 carries no Authorization, so AttachedCachedToken is false.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: throws the token-endpoint failure; the endpoint was contacted exactly once (the
        // initial follow) — recovery did not cold-probe and re-derive, which would fetch a second time.
        await Assert.ThrowsAsync<ResponseException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        VerifyTokenFetch(mockHandler, Times.Once());
    }

    [Fact]
    public async Task SendAsync_TokenEndpoint401OnStaleToken_WithoutRecovery_Throws()
    {
        // Arrange: identical stale-token + token-endpoint-401 scenario, but recovery is NOT configured
        // (default). Behavior must be preserved: the token-endpoint exception is thrown, no cold probe.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return Unauthorized(req, realm: null);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge for the stale token
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object)); // ChallengeRecovery == null
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: the token-endpoint failure surfaces exactly as before — recovery never masks it.
        await Assert.ThrowsAsync<ResponseException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_ConformantRegistry_RecoveryNeverFires()
    {
        // Arrange: a conformant registry (no cached token) with recovery configured. The first
        // (credential-free) request already yields a usable challenge, so recovery is never reached
        // and adds no extra request.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // normal challenge on the first request
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: 200, and the registry saw exactly one credential-free request (the initial one) —
        // recovery did not add a second cold probe.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_ColdProbeAlsoUnusable_GivesUpAfterSingleProbe()
    {
        // Arrange: pathological upstream — both the stale-token attempt AND the cold probe return a
        // no-challenge 401. Recovery must probe exactly once, then give up and return the original 401
        // (no infinite loop).
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            return Unauthorized(req, realm: null); // every registry request → no challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: original 401 returned; exactly one cold probe (bounded); no token fetch.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, 1)]        // token worked → sticky; the 2nd request skips recovery
    [InlineData(HttpStatusCode.Forbidden, 2)] // token rejected (403) → not sticky; 2nd request re-probes
    public async Task SendAsync_ChallengeRecovery_StickyOnlyAfterWorkingToken(
        HttpStatusCode recoveredTokenStatus, int expectedColdProbes)
    {
        // Arrange: a stale token provokes a no-challenge 401; recovery derives a fresh token that the
        // registry answers with `recoveredTokenStatus`. The stale scope key is replaced only when that
        // token actually worked (2xx/3xx), so a follow-up request re-enters recovery iff it didn't.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return new HttpResponseMessage(recoveredTokenStatus) { RequestMessage = req };
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, coldRealm); // cold probe → usable challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);

        // Act: two identical requests.
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);

        // Assert: both requests resolve to the same status; recovery cold-probed once (sticky) or on
        // both requests (not sticky), per whether the recovered token worked.
        Assert.Equal(recoveredTokenStatus, response1.StatusCode);
        Assert.Equal(recoveredTokenStatus, response2.StatusCode);
        VerifyColdProbe(mockHandler, Times.Exactly(expectedColdProbes));
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NonReplayableRequest_DoesNotColdProbe()
    {
        // Arrange: a POST (non-idempotent) with a stale token hits a no-challenge 401. ColdProbe must
        // decline (CanReplay=false) rather than replay a mutating request without authorization.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Ok(req); // an errant cold probe would look successful — so a 401 result proves none ran
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{host}{_requestPath}")
        {
            Content = new StringContent("{}")
        };

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the 401 is returned unchanged; ColdProbe declined without replaying.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_HandlerThrows_Propagates()
    {
        // Arrange: a recovery handler that throws must surface the exception (engine disposes the 401).
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            return Unauthorized(req, realm: null);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = new DelegateChallengeRecovery(
                (context, ct) => throw new InvalidOperationException("recovery boom"))
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        Assert.Equal("recovery boom", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NoCachedToken_ColdProbeDeclines()
    {
        // Arrange: an unusable 401 with NO cached token attached (first-request style). ColdProbe must
        // decline (AttachedCachedToken=false) rather than issue a redundant credential-free replay.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            return Unauthorized(req, realm: null); // every request → no challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        // No stale token seeded → attempt 1 carries no Authorization.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: original 401 returned; the registry saw exactly one credential-free request (the
        // initial attempt) — ColdProbe added no second one because AttachedCachedToken was false.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_StaleBasicCredentials_DoesNotRecover()
    {
        // Arrange: a cached Basic credential provokes a no-challenge 401. Recovery must NOT fire — a
        // credential-free cold probe cannot remedy Basic auth (the same credentials would just be
        // re-sent), so the seam is scoped to stale Bearer tokens.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            if (req.Headers.Authorization?.Scheme == "Basic") return Unauthorized(req, realm: null);
            return Ok(req); // an errant cold probe (no auth) would look successful
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        client.Cache.SetCache(host, Challenge.Scheme.Basic, string.Empty, "cached_basic_creds");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the 401 is returned unchanged; no cold probe (a 200 would have meant one ran).
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_CustomHandlerFirstContact_DoesNotPoisonEmptyScopeKey()
    {
        // Arrange: a custom handler recovers a first-contact 401 (no cached token was attached, so the
        // attempted scope key is empty). The freshly derived token must NOT be cached under that empty
        // key, or later no-scope requests would silently attach a scoped token. So each such request
        // re-enters recovery instead. (The built-in ColdProbe never hits this — it self-gates on
        // AttachedCachedToken — but a custom handler can, so the engine must not refresh the key.)
        var host = NewHost();
        var coldRealm = $"https://{host}/token";
        var recoveryCalls = 0;

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, realm: null); // first (no-auth) attempt → no-challenge 401
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            // A custom recovery that synthesizes a usable challenge regardless of the gating signals.
            ChallengeRecovery = new DelegateChallengeRecovery((context, ct) =>
            {
                recoveryCalls++;
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                challenge.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                    "Bearer", $"realm=\"{coldRealm}\",service=\"registry\",scope=\"{_scope}\""));
                return Task.FromResult<HttpResponseMessage?>(challenge);
            })
        };
        // No stale token seeded → attachedCachedToken is false and the attempted key is empty.

        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);

        // Assert: both succeed, but recovery ran on BOTH requests — the empty scope key was never
        // poisoned with the scoped token (which would have let the second request skip recovery).
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(2, recoveryCalls);
    }

    /// <summary>
    /// Test adapter that turns a lambda into an <see cref="IChallengeRecovery"/>, so tests can express
    /// ad-hoc recovery strategies inline.
    /// </summary>
    private sealed class DelegateChallengeRecovery : IChallengeRecovery
    {
        private readonly Func<FailedChallenge, CancellationToken, Task<HttpResponseMessage?>> _recover;

        public DelegateChallengeRecovery(
            Func<FailedChallenge, CancellationToken, Task<HttpResponseMessage?>> recover)
            => _recover = recover;

        public Task<HttpResponseMessage?> RecoverAsync(
            FailedChallenge context, CancellationToken cancellationToken = default)
            => _recover(context, cancellationToken);
    }
}
