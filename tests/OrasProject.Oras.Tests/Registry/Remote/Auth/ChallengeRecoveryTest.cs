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
/// mishandle a stale cached token (a registry that omits the challenge, or points its realm at a
/// different host), while proving the seam is inert for conformant registries and never masks a
/// genuine credential failure.
/// </summary>
public class ChallengeRecoveryTest
{
    private const string _staleToken = "stale_bearer_token";
    private const string _freshToken = "fresh_access_token";
    private const string _requestPath = "/v2/redis/manifests/8.6.4";
    private const string _scope = "repository:redis:pull";

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
        // Arrange: dhi.io-style — a stale cached token provokes a 401 with NO WWW-Authenticate
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
        // Arrange: identical to the dhi case, but recovery is NOT configured (default).
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
        // Arrange: nvcr.io-style — a stale cached token provokes a 401 whose realm points at a foreign
        // host (rejected by the realm validator), but a cold request yields a same-host (allowed) realm.
        var host = NewHost();
        var foreignRealm = "https://authn.nvidia.com/token";
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
        // Arrange: identical to the nvcr case, but recovery is NOT configured (default).
        var host = NewHost();
        var foreignRealm = "https://authn.nvidia.com/token";

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

    [Fact]
    public async Task SendAsync_ChallengeRecovery_AnonymousColdProbe_ReturnsResponseWithoutTokenFetch()
    {
        // Arrange: a stale token provokes a no-challenge 401, but the resource is actually served
        // anonymously — the cold probe returns 200 directly, so no token is fetched.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Ok(req); // cold probe (no auth) → anonymous 200
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

        // Assert: the anonymous 200 is returned, and no token endpoint was contacted.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_UsableChallengeButBadCredentials_DoesNotFireRecovery()
    {
        // Arrange: the stale token yields a perfectly usable challenge (allowed realm), but the token
        // endpoint rejects the credentials. Recovery must NOT fire and must NOT mask the failure.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge for stale token
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: the genuine token-endpoint failure surfaces; recovery never cold-probes.
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

    [Fact]
    public async Task SendAsync_ChallengeRecovery_AfterRecovery_SecondRequestSkipsColdProbe()
    {
        // Arrange: dhi.io-style stale-token-no-challenge. After the first request recovers, the fresh
        // token must replace the stale one under the originally attempted scope key, so a second request
        // succeeds directly without re-attaching the stale token or re-probing.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, coldRealm);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);

        // Act: first request recovers; second request reuses the refreshed cache.
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);

        // Assert: both succeed; the stale token was sent only once (first attempt) and the cold probe
        // fired only once — the second request used the now-cached fresh token directly.
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        VerifyStaleTokenSent(mockHandler); // Times.Once across both requests
        VerifyColdProbe(mockHandler, Times.Once());
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
            ChallengeRecovery = (context, ct) => throw new InvalidOperationException("recovery boom")
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
    public async Task SendAsync_ChallengeRecovery_NonSuccessColdProbe_ReturnsOriginal401()
    {
        // Arrange: stale-token no-challenge 401; the cold probe yields a non-401, non-success response
        // (404). That is not a recovery — the original 401 must be surfaced, not the 404.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req }; // cold → 404
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

        // Assert: the original 401 (not the cold 404) is returned; the probe fired once; no token fetch.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
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
    public async Task SendAsync_ChallengeRecovery_AnonymousColdProbeRedirect_ReturnsRedirect()
    {
        // Arrange: a stale token provokes a no-challenge 401, but the resource is served anonymously via
        // a redirect (e.g. a blob-location 307). The redirect must be returned to the caller — callers
        // like GetBlobLocationAsync (allowAutoRedirect:false) treat 3xx as the successful outcome.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _staleToken) return Unauthorized(req, realm: null);
            var redirect = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect) { RequestMessage = req };
            redirect.Headers.Location = new Uri($"https://cdn.example.com{_requestPath}");
            return redirect; // cold probe (no auth) → 307 with a location
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

        // Assert: the 307 (not the original 401) is returned; the probe fired once; no token fetch.
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_RecoveredTokenForbidden_DoesNotBecomeSticky()
    {
        // Arrange: recovery re-derives a token that the registry then rejects with 403 (not a success).
        // The stale scope key must NOT be refreshed with that token, so a second request re-enters
        // recovery rather than silently attaching a non-working token.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = req };
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, coldRealm); // cold probe → usable challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object))
        {
            ChallengeRecovery = ChallengeRecoveries.ColdProbe
        };
        SeedStaleToken(client, host);

        // Act: two requests.
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);

        // Assert: both get 403, and recovery ran on BOTH (cold probe twice) — the 403 token was never
        // made sticky under the stale scope key.
        Assert.Equal(HttpStatusCode.Forbidden, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, response2.StatusCode);
        VerifyColdProbe(mockHandler, Times.Exactly(2));
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
            // A custom handler that synthesizes a usable challenge regardless of the gating signals.
            ChallengeRecovery = (context, ct) =>
            {
                recoveryCalls++;
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                challenge.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                    "Bearer", $"realm=\"{coldRealm}\",service=\"registry\",scope=\"{_scope}\""));
                return Task.FromResult<HttpResponseMessage?>(challenge);
            }
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
}
