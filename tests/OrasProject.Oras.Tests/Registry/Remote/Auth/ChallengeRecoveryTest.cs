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
using Moq;
using Moq.Protected;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry.Remote.Exceptions;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

/// <summary>
/// Tests for built-in recovery from non-conformant upstreams that mishandle a stale cached token by
/// omitting the challenge, returning an unusable realm, or rejecting the derived token request. Recovery
/// runs only after standard challenge handling fails and remains inert for conformant registries.
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

    private static HttpResponseMessage Unauthorized(
        HttpRequestMessage req,
        string? realm,
        string scope = _scope)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
        if (realm != null)
        {
            response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", $"realm=\"{realm}\",service=\"registry\",scope=\"{scope}\""));
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
        => VerifyStaleTokenSent(mockHandler, Times.Once());

    private static void VerifyStaleTokenSent(
        Mock<System.Net.Http.DelegatingHandler> mockHandler,
        Times times)
        => mockHandler.Protected().Verify(
            "SendAsync", times,
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
        var client = new Client(new HttpClient(mockHandler.Object));
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
    public async Task SendAsync_ChallengeRecovery_ColdProbeCopiesRequestMetadata()
    {
        // Arrange: a cold OCI request is fresh and bodyless, while retaining the headers, options, and
        // HTTP version needed to request the same representation.
        var host = NewHost();
        var optionKey = new HttpRequestOptionsKey<string>("test-option");

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.Headers.Authorization?.Parameter == _staleToken)
            {
                return Unauthorized(req, realm: null);
            }

            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal($"https://{host}{_requestPath}", req.RequestUri!.ToString());
            Assert.Equal(HttpVersion.Version20, req.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, req.VersionPolicy);
            Assert.Contains("application/test", req.Headers.Accept.Select(value => value.MediaType));
            Assert.True(req.Options.TryGetValue(optionKey, out var optionValue));
            Assert.Equal("test-value", optionValue);
            Assert.Null(req.Content);
            return Ok(req);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        request.Headers.Accept.ParseAdd("application/test");
        request.Options.Set(optionKey, "test-value");

        // Act
        using var response = await client.SendAsync(
            request,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
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
        var client = new Client(new HttpClient(mockHandler.Object));
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

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_ChallengeRecovery_ColdProbeTerminalStatus_IsReturned(
        HttpStatusCode coldProbeStatus)
    {
        // Arrange: a stale token provokes a no-challenge 401; the credential-free cold probe comes back
        // with `coldProbeStatus`. Once entered, the cold path is authoritative, so any terminal response
        // is returned as-is and no token is fetched.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            if (req.Headers.Authorization?.Parameter == _staleToken) return Unauthorized(req, realm: null);
            return new HttpResponseMessage(coldProbeStatus) { RequestMessage = req }; // cold probe (no auth)
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(coldProbeStatus, response.StatusCode);
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
    public async Task SendAsync_ChallengeRecovery_AccessTokenProvider401_ColdProbeRecovers()
    {
        // Arrange: bearer acquisition through a custom provider rejects the stale-token challenge once.
        // The cold challenge retries the same provider, which then returns a usable fresh token.
        var host = NewHost();
        var realm = $"https://{host}/token";
        var providerFailure = new ResponseException(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, realm)
            });
        var accessTokenProvider = new Mock<IAccessTokenProvider>();
        accessTokenProvider
            .SetupSequence(provider => provider.ResolveAccessTokenAsync(
                host,
                realm,
                "registry",
                It.IsAny<IReadOnlyList<string>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(providerFailure)
            .ReturnsAsync(_freshToken);

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, realm);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(
            new HttpClient(mockHandler.Object),
            noRedirectHttpClient: null,
            credentialProvider: null,
            accessTokenProvider: accessTokenProvider.Object,
            cache: null);
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        using var response = await client.SendAsync(
            request,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
        accessTokenProvider.Verify(
            provider => provider.ResolveAccessTokenAsync(
                host,
                realm,
                "registry",
                It.IsAny<IReadOnlyList<string>>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TokenEndpoint401_ColdProbeAlsoFails_GivesUpAndRethrows()
    {
        // Arrange: a stale token yields a usable (allowed) challenge, but following it to the token
        // endpoint returns 401. The cold probe produces the same challenge, whose token endpoint is the
        // authoritative terminal failure.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return Unauthorized(req, realm: null);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge for both stale attempt and cold probe
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: callers receive the public ResponseException from the terminal cold token request.
        await Assert.ThrowsAsync<ResponseException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TokenEndpoint401_NoCachedToken_DoesNotProbe()
    {
        // Arrange: a genuine first-time failure has no cached token, so attempt 1 is credential-free.
        // The token endpoint rejects the usable challenge, but recovery must not replay a request that
        // did not carry a cached bearer token.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return Unauthorized(req, realm: null);
            return Unauthorized(req, $"https://{host}/token"); // usable challenge on the credential-free request
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        // No stale token is seeded, so attempt 1 carries no Authorization header.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act + Assert: throws the token-endpoint failure; the endpoint was contacted exactly once (the
        // initial follow) — recovery did not cold-probe and re-derive, which would fetch a second time.
        await Assert.ThrowsAsync<ResponseException>(
            () => client.SendAsync(request, cancellationToken: CancellationToken.None));
        VerifyTokenFetch(mockHandler, Times.Once());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_ConformantStaleToken_DoesNotColdProbe()
    {
        // Arrange: a conformant registry returns a usable challenge for the stale cached token.
        // Standard handling must refresh it without adding a credential-free cold probe.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, $"https://{host}/token"); // normal challenge on the first request
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the stale token was refreshed directly from its usable challenge.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_TrustedRealmWithCredentials_DoesNotColdProbe()
    {
        // Arrange: the stale-token challenge points to a trusted foreign realm that accepts the
        // configured Basic credentials. Standard handling must succeed before recovery is considered.
        var host = NewHost();
        var foreignAuthHost = "auth.example.com";
        var foreignRealm = $"https://{foreignAuthHost}/token";
        const string username = "test-user";
        const string password = "test-password";
        var expectedBasicToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        var sawExpectedCredentials = false;

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req))
            {
                sawExpectedCredentials =
                    req.Headers.Authorization?.Scheme == "Basic"
                    && req.Headers.Authorization.Parameter == expectedBasicToken;
                return TokenResponse(req);
            }

            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            return Unauthorized(req, foreignRealm);
        }

        var credentialProvider = new SingleRegistryCredentialProvider(
            host,
            new Credential(username, password));
        var mockHandler = CustomHandler(Handler);
        var client = new Client(
            new HttpClient(mockHandler.Object),
            credentialProvider)
        {
            RealmValidator = new DefaultRealmValidator
            {
                TrustedRealmHosts =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { foreignAuthHost }
            }
        };
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        using var response = await client.SendAsync(
            request,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sawExpectedCredentials);
        VerifyStaleTokenSent(mockHandler);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Once());
        VerifyFreshTokenSent(mockHandler);
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_ColdProbeAlsoUnusable_GivesUpAfterSingleProbe()
    {
        // Arrange: pathological upstream — both the stale-token attempt AND the cold probe return a
        // no-challenge 401. Recovery must probe exactly once, then return the cold path's terminal 401.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            return Unauthorized(req, realm: null); // every registry request → no challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: terminal cold 401 returned; exactly one cold probe (bounded); no token fetch.
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
        const string expandedScope = "repository:app:pull,push";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return new HttpResponseMessage(recoveredTokenStatus) { RequestMessage = req };
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, coldRealm, expandedScope); // cold probe → expanded challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        Assert.True(Scope.TryParse(_scope, out var attemptedScope));
        client.ScopeManager.SetScopeForRegistry(host, attemptedScope, null);
        client.Cache.SetCache(host, Challenge.Scheme.Bearer, _scope, _staleToken);

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
    public async Task SendAsync_ChallengeRecovery_NonPullMethod_DoesNotColdProbe()
    {
        // Arrange: a POST with a stale token hits a no-challenge 401. Built-in recovery is deliberately
        // limited to the GET/HEAD methods used by OCI pull operations.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Ok(req); // an errant cold probe would look successful — so a 401 result proves none ran
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{host}{_requestPath}")
        {
            Content = new StringContent("{}")
        };

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: the 401 is returned unchanged; no credential-free POST was sent.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Never());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NoRedirectDefaultAuthorization_BypassesRecovery()
    {
        // Arrange: default authorization belongs to the selected no-redirect client, not the SDK cache.
        // The SDK must send it directly and must not replace it or cold-probe around its 401.
        var host = NewHost();
        const string defaultToken = "caller-supplied-token";
        List<string?> observedTokens = [];

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            observedTokens.Add(req.Headers.Authorization?.Parameter);
            return Unauthorized(req, realm: null);
        }

        HttpResponseMessage BaseHandler(HttpRequestMessage req, CancellationToken ct)
            => throw new InvalidOperationException("The redirecting client must not be used.");

        var baseHandler = CustomHandler(BaseHandler);
        var noRedirectHandler = CustomHandler(Handler);
        var noRedirectClient = new HttpClient(noRedirectHandler.Object);
        noRedirectClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", defaultToken);
        var client = new Client(
            new HttpClient(baseHandler.Object),
            noRedirectClient,
            credentialProvider: null,
            cache: null);
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        using var response = await client.SendAsync(
            request,
            allowAutoRedirect: false,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([defaultToken], observedTokens);
        VerifyStaleTokenSent(noRedirectHandler, Times.Never());
        VerifyColdProbe(noRedirectHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_BaseClientDefaultAuthorization_PreservesNoRedirectBypass()
    {
        // Arrange: preserve the existing behavior where BaseClient default authorization bypasses SDK
        // authentication even when the request itself uses the no-redirect client.
        var host = NewHost();
        List<string?> observedTokens = [];

        HttpResponseMessage NoRedirectHandler(HttpRequestMessage req, CancellationToken ct)
        {
            observedTokens.Add(req.Headers.Authorization?.Parameter);
            return Unauthorized(req, realm: null);
        }

        HttpResponseMessage BaseHandler(HttpRequestMessage req, CancellationToken ct)
            => throw new InvalidOperationException("The redirecting client must not be used.");

        var baseHandler = CustomHandler(BaseHandler);
        var baseClient = new HttpClient(baseHandler.Object);
        baseClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "base-client-token");
        var noRedirectHandler = CustomHandler(NoRedirectHandler);
        var client = new Client(
            baseClient,
            new HttpClient(noRedirectHandler.Object),
            credentialProvider: null,
            cache: null);
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        using var response = await client.SendAsync(
            request,
            allowAutoRedirect: false,
            cancellationToken: CancellationToken.None);

        // Assert: the SDK cache was bypassed, preserving the pre-existing behavior.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([null], observedTokens);
        VerifyStaleTokenSent(noRedirectHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NoCachedToken_ColdProbeDeclines()
    {
        // Arrange: an unusable first-request 401 has no cached bearer token attached, so built-in
        // recovery must not issue a redundant credential-free replay.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            return Unauthorized(req, realm: null); // every request → no challenge
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        // No stale token seeded → attempt 1 carries no Authorization.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: only the initial credential-free request was sent.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Once());
        VerifyTokenFetch(mockHandler, Times.Never());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_StaleBasicCredentials_DoesNotRecover()
    {
        // Arrange: a cached Basic credential provokes a no-challenge 401. Recovery must not fire because
        // a credential-free cold probe cannot remedy Basic authentication.
        var host = NewHost();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            if (req.Headers.Authorization?.Scheme == "Basic") return Unauthorized(req, realm: null);
            return Ok(req); // an errant cold probe (no auth) would look successful
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
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
    public async Task SendAsync_ChallengeRecovery_EmptyAttemptedKey_DoesNotCacheScopedToken()
    {
        // Arrange: the stale token was cached before any repository scope was known. Recovery may cache
        // the fresh token under its scoped key, but must not copy it into the empty catch-all key.
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
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        using var response = await client.SendAsync(
            request,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(client.Cache.TryGetToken(
            host,
            Challenge.Scheme.Bearer,
            string.Empty,
            out var emptyScopeToken));
        Assert.Equal(_staleToken, emptyScopeToken);
        Assert.True(client.Cache.TryGetToken(
            host,
            Challenge.Scheme.Bearer,
            _scope,
            out var scopedToken));
        Assert.Equal(_freshToken, scopedToken);
        VerifyColdProbe(mockHandler, Times.Once());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_OpaqueScope_DoesNotRefreshAttemptedKey()
    {
        // Arrange: the caller's structured scope drives the attempted cache key. A stale token under
        // that key provokes recovery, whose cold-probe challenge adds an opaque scope. Tokens fetched
        // for opaque scopes are deliberately non-cacheable, including under the attempted key.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";
        const string opaqueScope = "opaque-scope";
        var tokenRequestCount = 0;
        var currentToken = string.Empty;

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req))
            {
                currentToken = $"opaque_scope_token_{++tokenRequestCount}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"access_token\":\"{currentToken}\"}}"),
                    RequestMessage = req
                };
            }

            var token = req.Headers.Authorization?.Parameter;
            if (!string.IsNullOrEmpty(currentToken) && token == currentToken) return Ok(req);
            if (token == _staleToken) return Unauthorized(req, realm: null);
            return Unauthorized(req, coldRealm, opaqueScope);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        Assert.True(Scope.TryParse(_scope, out var structuredScope));
        client.ScopeManager.SetScopeForRegistry(host, structuredScope, null);
        client.Cache.SetCache(host, Challenge.Scheme.Bearer, _scope, _staleToken);

        // Act: both calls must recover independently; the first opaque-scope token must not become
        // sticky under the structured attempted key.
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");
        var response2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(2, tokenRequestCount);
        VerifyColdProbe(mockHandler, Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_NoChallenge_DisposesFirst401BeforeColdProbe()
    {
        // Arrange: the first no-challenge 401 owns an unread, unknown-length body. Once the cold path is
        // selected, that response must be disposed rather than buffered or preserved.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";
        var unauthorizedContent = new DisposalTrackingContent();

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken)
            {
                var response = Unauthorized(req, realm: null);
                response.Content = unauthorizedContent;
                return response;
            }

            Assert.True(unauthorizedContent.WasDisposed);
            return Unauthorized(req, coldRealm);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(unauthorizedContent.WasDisposed);
        VerifyColdProbe(mockHandler, Times.Once());
    }

    [Fact]
    public async Task SendAsync_ChallengeRecovery_HeadWithLargeDeclaredLength_ColdProbeRecovers()
    {
        // Arrange: HEAD responses can advertise the GET representation's large Content-Length. Recovery
        // disposes that failed response and does not attempt to buffer the advertised representation.
        var host = NewHost();
        var coldRealm = $"https://{host}/token";

        HttpResponseMessage Handler(HttpRequestMessage req, CancellationToken ct)
        {
            if (IsTokenEndpoint(req)) return TokenResponse(req);
            var token = req.Headers.Authorization?.Parameter;
            if (token == _freshToken) return Ok(req);
            if (token == _staleToken)
            {
                var response = Unauthorized(req, realm: null);
                response.Content = new LargeDeclaredLengthContent();
                return response;
            }

            return Unauthorized(req, coldRealm);
        }

        var mockHandler = CustomHandler(Handler);
        var client = new Client(new HttpClient(mockHandler.Object));
        SeedStaleToken(client, host);
        using var request = new HttpRequestMessage(HttpMethod.Head, $"https://{host}{_requestPath}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert: recovery proceeds without trying to buffer the nonexistent HEAD response body.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyColdProbe(mockHandler, Times.Once());
    }

    private sealed class DisposalTrackingContent : HttpContent
    {
        public bool WasDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class LargeDeclaredLengthContent : HttpContent
    {
        public LargeDeclaredLengthContent()
        {
            Headers.ContentLength = Client.MaxBufferSize + 1;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new InvalidOperationException("HEAD response content must not be buffered.");

        protected override bool TryComputeLength(out long length)
        {
            length = Client.MaxBufferSize + 1;
            return true;
        }
    }
}
