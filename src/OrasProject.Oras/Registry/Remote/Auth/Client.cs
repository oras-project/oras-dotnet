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

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Client provides authenticated access to OCI registries with automatic token management.
/// </summary>
public class Client : IClient
{
    #region private members
    /// <summary>
    /// Lazy singleton memory cache for scenarios where no IMemoryCache is injected.
    /// </summary>
    private static readonly Lazy<IMemoryCache> _sharedMemoryCache =
        new(() => new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024, // cache at most 1024 entries
        }), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Cache used for storing and retrieving authentication tokens
    /// to optimize remote registry operations.
    /// </summary>
    private ICache? _cache;

    /// <summary>
    /// Object used for locking during cache initialization.
    /// </summary>
    private readonly object _cacheLock = new();
    #endregion

    /// <summary>
    /// Initializes a new instance of the Client class.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow redirects.
    /// If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="credentialProvider">Optional credential provider for registry authentication.</param>
    /// <param name="cache">Optional cache for storing authentication tokens.</param>
    public Client(
        HttpClient? httpClient = null,
        ICredentialProvider? credentialProvider = null,
        ICache? cache = null)
        : this(httpClient, null, credentialProvider, cache)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Client class with separate HttpClient instances for redirect control.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow redirects.
    /// If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with <c>AllowAutoRedirect = false</c> for capturing redirect locations.
    /// If not provided, uses <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// <para>
    /// <strong>Advanced Usage:</strong> To apply consistent HTTP configuration (timeouts, proxy, headers) 
    /// across both redirect and no-redirect scenarios, provide both <paramref name="httpClient"/> and 
    /// <paramref name="noRedirectHttpClient"/> with the same base configuration but different redirect settings.
    /// This is useful with IHttpClientFactory or custom HttpClient management.
    /// </para>
    /// </param>
    /// <param name="credentialProvider">Optional credential provider for registry authentication.</param>
    /// <param name="cache">Optional cache for storing authentication tokens.</param>
    public Client(
        HttpClient? httpClient,
        HttpClient? noRedirectHttpClient,
        ICredentialProvider? credentialProvider,
        ICache? cache)
    {
        CredentialProvider = credentialProvider;
        _cache = cache;
        BaseClient = httpClient ?? DefaultHttpClient.Instance;
        NoRedirectClient = noRedirectHttpClient ?? DefaultHttpClient.NoRedirectInstance;
    }

    /// <summary>
    /// CredentialProvider provides the mechanism to retrieve
    /// credentials for accessing remote registries.
    /// </summary>
    public ICredentialProvider? CredentialProvider { get; init; }

    /// <summary>
    /// BaseClient is an instance of HttpClient to send http requests that follow redirects.
    /// </summary>
    public HttpClient BaseClient { get; }

    /// <summary>
    /// NoRedirectClient is an instance of HttpClient configured to not follow redirects.
    /// Used for scenarios where we need to capture redirect locations (e.g., blob location URLs).
    /// </summary>
    internal HttpClient NoRedirectClient { get; }

    /// <summary>
    /// Cache used for storing and retrieving authentication tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If no <see cref="ICache"/> is provided during construction, a default <see cref="Cache"/> 
    /// implementation is created using a shared <see cref="MemoryCache"/> instance with a size 
    /// limit of 1024 entries.
    /// </para>
    /// <para>
    /// The shared memory cache uses a size-based eviction policy, where each cache entry counts
    /// as 1 unit of size by default. When the cache reaches its limit of 1024 entries, the least
    /// recently used entries will be evicted.
    /// </para>
    /// <para>
    /// To customize caching behavior, you can either:
    /// <list type="bullet">
    /// <item>
    /// <description>Provide your own <see cref="ICache"/> implementation in the constructor</description>
    /// </item>
    /// <item>
    /// <description>Configure <see cref="Cache.CacheEntryOptions"/> on the default implementation
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public ICache Cache
    {
        get
        {
            if (_cache == null)
            {
                lock (_cacheLock)
                {
                    _cache ??= new Cache(_sharedMemoryCache.Value);
                }
            }
            return _cache;
        }
        set => _cache = value;
    }

    /// <summary>
    /// ClientId used in fetching OAuth2 token as a required field.
    /// If empty, a default client ID is used.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Determines whether to enforce OAuth2 authentication with the password grant type
    /// instead of using the distribution specification when authenticating with a username
    /// and password.
    /// </summary>
    public bool ForceAttemptOAuth2 { get; set; }

    /// <summary>
    /// defaultClientID specifies the default client ID used in OAuth2.
    /// </summary>
    private const string _defaultClientId = "oras-dotnet";

    /// <summary>
    /// ScopeManager is an instance to manage scopes.
    /// </summary>
    public ScopeManager ScopeManager { get; set; } = new();

    /// <summary>
    /// CustomHeaders is for users to customize headers
    /// </summary>
    public ConcurrentDictionary<string, List<string>> CustomHeaders { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// SetUserAgent is to set customized user agent per user requests.
    /// </summary>
    /// <param name="userAgent"></param>
    public void SetUserAgent(string userAgent)
    {
        if (CustomHeaders.TryGetValue("User-Agent", out var userAgents))
        {
            userAgents.Add(userAgent);
        }
        else
        {
            CustomHeaders["User-Agent"] = [userAgent];
        }
    }

    /// <summary>
    /// Asynchronously resolves the credential for the specified registry host.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="registry">The registry hostname to retrieve credentials for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved
    /// credential. If <see cref="CredentialProvider"/> is null, an empty credential is returned.
    /// </returns>
    public Task<Credential> ResolveCredentialAsync(
        string registry,
        CancellationToken cancellationToken = default)
        => CredentialProvider == null
            ? Task.FromResult(CredentialExtensions.EmptyCredential)
            : CredentialProvider.ResolveCredentialAsync(registry, cancellationToken);

    /// <summary>
    /// SendAsync sends an HTTP request asynchronously, attempting to resolve authentication if
    /// 'Authorization' header is not set.
    /// 
    /// This method intercepts the HTTP request to handle authentication challenges, including
    /// Basic and Bearer schemes. It attempts to retrieve cached tokens for the request's host
    /// and applies them to the request headers. If the server responds with an unauthorized
    /// status, it parses the authentication challenge and fetches new tokens as needed,
    /// updating the cache accordingly.
    /// </summary>
    /// <param name="originalRequest">The original HTTP request message to send.</param>
    /// <param name="tenantId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-tenant scenarios where different credentials are used for the same
    /// registry.
    /// </param>
    /// <param name="allowAutoRedirect">
    /// Whether to follow redirects automatically. Set to false to capture redirect URLs
    /// (e.g., for blob locations).
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP
    /// response message.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if the request URI is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if required parameters (e.g., "realm") are missing in the authentication challenge.
    /// </exception>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage originalRequest,
        string? tenantId = null,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(originalRequest, tenantId, allowAutoRedirect, cancellationToken);

    /// <summary>
    /// Fetches the Basic Authentication token for the specified registry host.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A Base64-encoded string representing the Basic Authentication token in the format
    /// "username:password".
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or when the username or password is null or empty.
    /// </exception>
    internal async Task<string> FetchBasicAuthAsync(
        string registry,
        CancellationToken cancellationToken = default)
    {
        var credential = await ResolveCredentialAsync(registry, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.Username) ||
            string.IsNullOrWhiteSpace(credential.Password))
        {
            throw new AuthenticationException(
                "Missing username or password for basic authentication.");
        }

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"));
    }

    /// <summary>
    /// Fetches a OAuth2 access token for accessing a registry.
    ///
    /// If credential is empty or refreshToken is not set and OAuth2 authentication is not forced,
    /// the method would fetch anonymous token with a Http Get request
    /// otherwise, it would fetch OAuth2 token with a Http Post request.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="realm">The authentication realm to use.</param>
    /// <param name="service">The service name for which the token is requested.</param>
    /// <param name="scopes">The scopes of access requested for the token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the bearer
    /// authentication token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or invalid.
    /// </exception>
    internal async Task<string> FetchBearerAuthAsync(
        string registry,
        string realm,
        string service,
        IList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var credential = await ResolveCredentialAsync(registry, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(credential.AccessToken))
        {
            return credential.AccessToken;
        }
        if (credential.IsEmpty() ||
            (string.IsNullOrWhiteSpace(credential.RefreshToken) && !ForceAttemptOAuth2))
        {
            return await FetchDistributionTokenAsync(
                realm,
                service,
                scopes,
                credential.Username,
                credential.Password,
                cancellationToken
            ).ConfigureAwait(false);
        }

        return await FetchOauth2TokenAsync(
            realm,
            service,
            scopes,
            credential,
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// FetchDistributionToken fetches a distribution access token from the specified authentication realm with a HTTP Get request.
    /// It fetches anonymous tokens if no credential is provided.
    ///
    /// </summary>
    /// <param name="realm">The authentication realm URL.</param>
    /// <param name="service">The service name for which the token is requested.</param>
    /// <param name="scopes">A collection of scopes defining the access permissions.</param>
    /// <param name="username">The username for basic authentication (optional).</param>
    /// <param name="password">The password for basic authentication (optional).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the fetched token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when both "access_token" and "token" are missing or empty in the response.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown when the HTTP request fails or the response status code is not OK.
    /// </exception>
    internal async Task<string> FetchDistributionTokenAsync(
        string realm,
        string service,
        IList<string> scopes,
        string? username,
        string? password,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, realm);
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        var queryParameters = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(service))
        {
            queryParameters.Add(new KeyValuePair<string, string>("service", service));
        }

        foreach (var scope in scopes)
        {
            queryParameters.Add(new KeyValuePair<string, string>("scope", scope));
        }

        var queryString = string.Join("&", queryParameters.Select(param => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));
        var uriBuilder = new UriBuilder(request.RequestUri!)
        {
            Query = queryString
        };
        request.RequestUri = uriBuilder.Uri;

        using var response = await SendRequestAsync(request, allowAutoRedirect: true, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("access_token", out var accessToken) && !string.IsNullOrWhiteSpace(accessToken.GetString()))
        {
            return accessToken.GetString()!;
        }

        if (root.TryGetProperty("token", out var token) && !string.IsNullOrWhiteSpace(token.GetString()))
        {
            return token.GetString()!;
        }

        throw new AuthenticationException("Both AccessToken and Token are empty or missing");
    }

    /// <summary>
    /// Fetches an OAuth2 access token from the specified authentication realm.
    /// </summary>
    /// <param name="realm">The authentication realm URL to send the token request to.</param>
    /// <param name="service">The service identifier for which the token is requested.</param>
    /// <param name="scopes">A list of scopes to request for the token.</param>
    /// <param name="credential">The credentials containing either a refresh token or username and password.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the fetched OAuth2 access token as a string.</returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when the credentials are missing or invalid, or if the access token is empty or missing.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown when there is an issue with the HTTP request or response.
    /// </exception>
    internal async Task<string> FetchOauth2TokenAsync(
        string realm,
        string service,
        IList<string> scopes,
        Credential credential,
        CancellationToken cancellationToken = default
    )
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = string.IsNullOrEmpty(ClientId) ? _defaultClientId : ClientId
        };

        if (!string.IsNullOrWhiteSpace(service))
        {
            form["service"] = service;
        }

        if (!string.IsNullOrEmpty(credential.RefreshToken))
        {
            form["grant_type"] = "refresh_token";
            form["refresh_token"] = credential.RefreshToken;
        }
        else if (!string.IsNullOrEmpty(credential.Username) && !string.IsNullOrEmpty(credential.Password))
        {
            form["grant_type"] = "password";
            form["username"] = credential.Username;
            form["password"] = credential.Password;
        }
        else
        {
            throw new AuthenticationException("missing username or password for bearer auth");
        }

        if (scopes.Count > 0)
        {
            form["scope"] = string.Join(" ", scopes);
        }
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, realm);
        request.Content = content;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.FormUrlEncoded);

        using var response = await SendRequestAsync(request, allowAutoRedirect: true, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("access_token", out var accessToken) && !string.IsNullOrEmpty(accessToken.ToString()))
        {
            return accessToken.ToString();
        }

        throw new AuthenticationException("AccessToken is empty or missing");
    }

    /// <summary>
    /// Core implementation for sending HTTP requests with authentication and optional redirect control.
    /// This method handles the complete authentication flow including cached token retrieval,
    /// challenge parsing, and credential fetching for both Basic and Bearer schemes.
    /// </summary>
    /// <param name="originalRequest">The original HTTP request message to send.</param>
    /// <param name="tenantId">Optional cache partition identifier for multi-tenant isolation.</param>
    /// <param name="allowAutoRedirect">Whether to allow automatic redirect following.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP response message.
    /// </returns>
    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage originalRequest,
        string? tenantId,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        foreach (var (headerName, headerValues) in CustomHeaders)
        {
            originalRequest.Headers.TryAddWithoutValidation(headerName, headerValues);
        }

        originalRequest.AddDefaultUserAgent();
        if (originalRequest.Headers.Authorization != null || BaseClient.DefaultRequestHeaders.Authorization != null)
        {
            return await SendRequestAsync(originalRequest, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
        }
        var host = originalRequest.RequestUri?.Authority ??
                    throw new ArgumentException("originalRequest.RequestUri or originalRequest.RequestUri.Authority property is null.", nameof(originalRequest));
        var requestAttempt1 = await originalRequest.CloneAsync(rewindContent: false, cancellationToken).ConfigureAwait(false);
        var attemptedKey = string.Empty;

        // attempt to send request with cached auth token
        if (Cache.TryGetScheme(host, out var schemeFromCache, tenantId))
        {
            switch (schemeFromCache)
            {
                case Challenge.Scheme.Basic:
                    {
                        if (Cache.TryGetToken(host, schemeFromCache, string.Empty, out var basicToken, tenantId))
                        {
                            requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                        }

                        break;
                    }
                case Challenge.Scheme.Bearer:
                    {
                        var scopes = ScopeManager.GetScopesStringForHost(host);
                        attemptedKey = string.Join(" ", scopes);
                        if (Cache.TryGetToken(host, schemeFromCache, attemptedKey, out var bearerToken, tenantId))
                        {
                            requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                        }
                        break;
                    }
            }
        }

        var response1 = await SendRequestAsync(requestAttempt1, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
        if (response1.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response1;
        }

        var (schemeFromChallenge, parameters) =
            Challenge.ParseChallenge(response1.Headers.WwwAuthenticate.FirstOrDefault()?.ToString());

        // Attempt again with credentials for recognized schemes
        switch (schemeFromChallenge)
        {
            case Challenge.Scheme.Basic:
                {
                    response1.Dispose();
                    var basicAuthToken = await FetchBasicAuthAsync(host, cancellationToken).ConfigureAwait(false);
                    Cache.SetCache(host, schemeFromChallenge, string.Empty, basicAuthToken, tenantId);

                    // Attempt again with basic token
                    var requestAttempt2 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                    requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
                    return await SendRequestAsync(requestAttempt2, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
                }
            case Challenge.Scheme.Bearer:
                {
                    response1.Dispose();
                    if (parameters == null)
                    {
                        throw new AuthenticationException("Missing parameters in the Www-Authenticate challenge.");
                    }

                    var existingScopes = ScopeManager.GetScopesForHost(host);
                    var newScopes = new SortedSet<Scope>(existingScopes);
                    if (parameters.TryGetValue("scope", out var scopesString))
                    {
                        foreach (var scopeStr in scopesString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (Scope.TryParse(scopeStr, out var scope))
                            {
                                Scope.AddOrMergeScope(newScopes, scope);
                            }
                        }
                    }

                    // Attempt to send request when the scope changes and a token cache hits
                    var newKey = string.Join(" ", newScopes);
                    if (newKey != attemptedKey &&
                        Cache.TryGetToken(host, schemeFromChallenge, newKey, out var cachedToken, tenantId))
                    {
                        var requestAttempt2 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                        requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
                        var response2 = await SendRequestAsync(requestAttempt2, allowAutoRedirect, cancellationToken).ConfigureAwait(false);

                        if (response2.StatusCode != HttpStatusCode.Unauthorized)
                        {
                            return response2;
                        }
                        response2.Dispose();
                    }

                    if (!parameters.TryGetValue("realm", out var realm))
                    {
                        // 'realm' is required as it specifies the token endpoint URL for Bearer authentication.
                        throw new KeyNotFoundException("Missing 'realm' parameter in WWW-Authenticate Bearer challenge.");
                    }
                    if (!parameters.TryGetValue("service", out var service))
                    {
                        // some registries may omit the `service` parameter; use an empty string when absent.
                        service = string.Empty;
                    }

                    // Try to fetch bearer token based on the challenge header
                    var bearerAuthToken = await FetchBearerAuthAsync(
                        host,
                        realm,
                        service,
                        newScopes.Select(newScope => newScope.ToString()).ToList(),
                        cancellationToken
                    ).ConfigureAwait(false);
                    Cache.SetCache(host, schemeFromChallenge, newKey, bearerAuthToken, tenantId);

                    var requestAttempt3 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                    requestAttempt3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAuthToken);
                    return await SendRequestAsync(requestAttempt3, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
                }
            default:
                return response1;
        }
    }

    /// <summary>
    /// Sends an HTTP request using the configured HttpClient with optional redirect control.
    /// HttpCompletionOption.ResponseHeadersRead is used here to enable content streaming and to 
    /// avoid buffering the entire response body in memory.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="allowAutoRedirect">Whether to allow automatic redirect following. Default is true.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The HTTP response message.</returns>
    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default)
    {
        var client = allowAutoRedirect ? BaseClient : NoRedirectClient;
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }
}
