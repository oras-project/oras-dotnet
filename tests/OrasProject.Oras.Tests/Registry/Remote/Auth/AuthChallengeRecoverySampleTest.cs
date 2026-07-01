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
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Examples.AuthChallengeRecovery;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

/// <summary>
/// Validates the <see cref="IAuthChallengeHandler"/> contract against the two real-world
/// recovery scenarios it was designed for, implemented using ONLY the public API:
/// <list type="bullet">
/// <item>a registry that omits the challenge entirely on a stale-token 401, and</item>
/// <item>a registry whose stale-token 401 challenge points at a foreign (different-host) realm.</item>
/// </list>
/// The <see cref="ColdReDeriveAuthChallengeHandler"/> below is a liftable reference
/// implementation a pull-through-cache consumer would supply.
/// </summary>
public class AuthChallengeRecoverySampleTest
{
    private const string _staleToken = "stale_token";
    private const string _freshToken = "fresh_token";

    // ---- Scenario A: registry omits the challenge on a stale-token 401 (dhi-style) ----

    [Fact]
    public async Task Recovers_When_StaleToken401_HasNoChallenge()
    {
        const string host = "registry-omits-challenge.example.com";

        HttpResponseMessage Registry(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.RequestUri!.AbsolutePath == "/token")
            {
                return Json($"{{\"access_token\":\"{_freshToken}\"}}");
            }

            return req.Headers.Authorization?.Parameter switch
            {
                _freshToken => new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req },
                // Stale token -> 401 with NO WWW-Authenticate challenge.
                _staleToken => new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req },
                // Cold (no-auth) request -> a usable, same-host Bearer challenge.
                _ => Unauthorized(req, $"Bearer realm=\"https://{host}/token\",service=\"{host}\""),
            };
        }

        var response = await RunWithStaleToken(host, Registry);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Scenario B: stale-token 401 challenge points at a foreign realm (nvcr-style) ----

    [Fact]
    public async Task Recovers_When_StaleToken401_PointsAtForeignRealm()
    {
        const string host = "registry-foreign-realm.example.com";
        const string foreignIdp = "idp.example.com"; // different host -> realm validation denies it

        HttpResponseMessage Registry(HttpRequestMessage req, CancellationToken ct)
        {
            // The cold, same-host realm endpoint hands out a fresh token.
            if (req.RequestUri!.AbsolutePath == "/proxy_auth")
            {
                return Json($"{{\"access_token\":\"{_freshToken}\"}}");
            }

            return req.Headers.Authorization?.Parameter switch
            {
                _freshToken => new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req },
                // Stale token -> 401 whose realm is a FOREIGN host (its IdP).
                _staleToken => Unauthorized(req, $"Bearer realm=\"https://{foreignIdp}/token\",service=\"{host}\""),
                // Cold (no-auth) request -> a usable, SAME-host Bearer challenge.
                _ => Unauthorized(req, $"Bearer realm=\"https://{host}/proxy_auth\",service=\"{host}\""),
            };
        }

        var response = await RunWithStaleToken(host, Registry);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Compliant registry: handler defers to the standard flow, no cold probe ----

    [Fact]
    public async Task CompliantRegistry_SucceedsViaStandardFlow_WithoutColdProbe()
    {
        const string host = "registry-compliant.example.com";
        var requests = new List<string>();

        HttpResponseMessage Registry(HttpRequestMessage req, CancellationToken ct)
        {
            requests.Add($"{req.Method} {req.RequestUri!.AbsolutePath} auth={req.Headers.Authorization?.Parameter ?? "none"}");
            if (req.RequestUri!.AbsolutePath == "/token")
            {
                return Json($"{{\"access_token\":\"{_freshToken}\"}}");
            }

            return req.Headers.Authorization?.Parameter == _freshToken
                ? new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req }
                : Unauthorized(req, $"Bearer realm=\"https://{host}/token\",service=\"{host}\"");
        }

        var client = new Client(new HttpClient(CustomHandler(Registry).Object))
        {
            AuthChallengeHandler = new ColdReDeriveAuthChallengeHandler(),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/v2/");

        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // attempt-1 (401) + token fetch + retry (200) == 3 requests; a cold probe would add a 4th.
        Assert.Equal(3, requests.Count);
    }

    // ---- Credential failure behind an ALLOWED realm must NOT be masked by recovery ----

    [Fact]
    public async Task CredentialFailure_BehindAllowedRealm_IsNotMaskedByRecovery()
    {
        const string host = "registry-badcreds.example.com";

        HttpResponseMessage Registry(HttpRequestMessage req, CancellationToken ct)
        {
            // Token endpoint rejects the credentials.
            if (req.RequestUri!.AbsolutePath == "/token")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
            }

            // Same-host (allowed) realm — so this is a credential failure, not a challenge failure.
            return Unauthorized(req, $"Bearer realm=\"https://{host}/token\",service=\"{host}\"");
        }

        // The token fetch failure must surface (not be swallowed and retried as a cold probe).
        await Assert.ThrowsAnyAsync<Exception>(() => RunWithStaleToken(host, Registry));
    }

    // ---- helpers ----

    private static Task<HttpResponseMessage> RunWithStaleToken(
        string host,
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> registry)
    {
        var client = new Client(new HttpClient(CustomHandler(registry).Object))
        {
            AuthChallengeHandler = new ColdReDeriveAuthChallengeHandler(),
        };
        // Seed a stale token so attempt-1 attaches it (AttachedCachedToken == true).
        client.Cache.SetCache(host, Challenge.Scheme.Bearer, string.Empty, _staleToken);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/v2/");
        return client.SendAsync(request, cancellationToken: CancellationToken.None);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Unauthorized(HttpRequestMessage req, string wwwAuthenticate)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = req };
        var space = wwwAuthenticate.IndexOf(' ');
        response.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(wwwAuthenticate[..space], wwwAuthenticate[(space + 1)..]));
        return response;
    }
}
