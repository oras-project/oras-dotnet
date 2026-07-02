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
using System.IO;
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
        : this(httpClient, null, credentialProvider, null, cache)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Client class with separate
    /// HttpClient instances for redirect control.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow
    /// redirects.  If not provided, uses
    /// <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with
    /// <c>AllowAutoRedirect = false</c> for capturing redirect
    /// locations.  If not provided, uses
    /// <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// <para>
    /// <strong>Advanced Usage:</strong> To apply consistent HTTP
    /// configuration (timeouts, proxy, headers) across both redirect
    /// and no-redirect scenarios, provide both
    /// <paramref name="httpClient"/> and
    /// <paramref name="noRedirectHttpClient"/> with the same base
    /// configuration but different redirect settings.  This is useful
    /// with IHttpClientFactory or custom HttpClient management.
    /// </para>
    /// </param>
    /// <param name="credentialProvider">
    /// Optional credential provider for registry authentication.
    /// </param>
    /// <param name="cache">
    /// Optional cache for storing authentication tokens.
    /// </param>
    public Client(
        HttpClient? httpClient,
        HttpClient? noRedirectHttpClient,
        ICredentialProvider? credentialProvider,
        ICache? cache)
        : this(httpClient, noRedirectHttpClient, credentialProvider,
            null, cache)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Client class with separate
    /// HttpClient instances for redirect control and an optional
    /// access-token provider.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow
    /// redirects.  If not provided, uses
    /// <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with
    /// <c>AllowAutoRedirect = false</c> for capturing redirect
    /// locations.  If not provided, uses
    /// <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// </param>
    /// <param name="credentialProvider">
    /// Optional credential provider for registry authentication.
    /// </param>
    /// <param name="accessTokenProvider">
    /// Optional access-token provider.  When set, the client calls
    /// this provider first during Bearer authentication.  If it
    /// returns <c>null</c> or whitespace, the client falls through to
    /// credential-based authentication via
    /// <paramref name="credentialProvider"/>.
    /// </param>
    /// <param name="cache">
    /// Optional cache for storing authentication tokens.
    /// </param>
    public Client(
        HttpClient? httpClient,
        HttpClient? noRedirectHttpClient,
        ICredentialProvider? credentialProvider,
        IAccessTokenProvider? accessTokenProvider,
        ICache? cache)
    {
        CredentialProvider = credentialProvider;
        AccessTokenProvider = accessTokenProvider;
        _cache = cache;
        BaseClient = httpClient ?? DefaultHttpClient.Instance;
        NoRedirectClient =
            noRedirectHttpClient ?? DefaultHttpClient.NoRedirectInstance;
    }

    /// <summary>
    /// CredentialProvider provides the mechanism to retrieve
    /// credentials for accessing remote registries.
    /// </summary>
    public ICredentialProvider? CredentialProvider { get; init; }

    /// <summary>
    /// AccessTokenProvider provides pre-resolved access tokens for
    /// Bearer authentication.  When set, the client calls this provider
    /// first during Bearer auth.  If it returns <c>null</c> or
    /// whitespace, the client falls through to credential-based
    /// authentication via <see cref="CredentialProvider"/>.
    /// </summary>
    public IAccessTokenProvider? AccessTokenProvider { get; init; }

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
    /// Maximum size (4 MB) for buffering non-seekable request content.
    /// In practice only manifest-sized payloads flow through this path.
    /// </summary>
    internal const long MaxBufferSize = 4 * 1024 * 1024;

    /// <summary>
    /// Validates realm URLs before sending credentials to them.
    /// Default: a <see cref="DefaultRealmValidator"/> instance with
    /// secure defaults.
    /// </summary>
    /// <remarks>
    /// This property is init-only and cannot be null. To use a
    /// different validator, set it during construction. To disable
    /// validation (not recommended), provide an implementation
    /// that always returns <c>true</c>.
    /// </remarks>
    public IRealmValidator RealmValidator
    {
        get => _realmValidator;
        init => _realmValidator = value
            ?? throw new ArgumentNullException(nameof(value));
    }

    private IRealmValidator _realmValidator = new DefaultRealmValidator();

    /// <summary>
    /// An optional recovery handler consulted when standard resolution of a 401 fails to produce a
    /// usable challenge (e.g. a non-conformant registry that omits the challenge on a token-carrying
    /// 401, or points its realm at a different host). Defaults to <c>null</c> — no recovery, i.e. the
    /// standard give-up behavior. Set to <see cref="ChallengeRecoveries.ColdProbe"/> (or a custom
    /// handler) to opt in.
    /// </summary>
    public ChallengeRecoveryHandler? ChallengeRecovery { get; init; }

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
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same
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
    /// <exception cref="AuthenticationException">
    /// Thrown when the realm URL is invalid or not allowed by
    /// <see cref="RealmValidator"/>.
    /// </exception>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage originalRequest,
        string? partitionId = null,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(originalRequest, partitionId, allowAutoRedirect, cancellationToken);

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
    /// Fetches a bearer access token for accessing a registry.
    ///
    /// When an <see cref="AccessTokenProvider"/> is configured it is
    /// consulted first.  If it returns a non-whitespace token, that
    /// token is used directly — bypassing credential resolution
    /// entirely.  Otherwise, the existing credential-based flow is
    /// used: if credential is empty or refreshToken is not set and
    /// OAuth2 authentication is not forced, the method would fetch
    /// anonymous token with a Http Get request otherwise, it would
    /// fetch OAuth2 token with a Http Post request.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="realm">The authentication realm to use.</param>
    /// <param name="service">
    /// The service name for which the token is requested.
    /// </param>
    /// <param name="scopes">
    /// The scopes of access requested for the token.
    /// </param>
    /// <param name="forceRefresh">
    /// When <see langword="true"/>, instructs the provider to bypass
    /// any cached token and acquire a fresh one. This is always set
    /// after a 401 response indicates the current token is stale.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task
    /// result contains the bearer authentication token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or invalid.
    /// </exception>
    internal async Task<string> FetchBearerAuthAsync(
        string registry,
        string realm,
        string service,
        IList<string> scopes,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // When an AccessTokenProvider is configured, try it first.
        // It returns a ready-to-use scoped token without exposing raw
        // credentials in-process.
        if (AccessTokenProvider != null)
        {
            var readOnlyScopes = scopes as IReadOnlyList<string>
                ?? (IReadOnlyList<string>)scopes.ToArray();
            var accessToken =
                await AccessTokenProvider.ResolveAccessTokenAsync(
                    registry,
                    realm,
                    service,
                    readOnlyScopes,
                    forceRefresh,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return accessToken;
            }
        }

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
    /// <param name="partitionId">Optional cache partition identifier for multi-partition isolation.</param>
    /// <param name="allowAutoRedirect">Whether to allow automatic redirect following.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP response message.
    /// </returns>
    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage originalRequest,
        string? partitionId,
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

        // Buffer non-seekable content upfront so auth retries can
        // rewind it.  Skips when Content-Length exceeds 4 MB (to
        // avoid consuming large non-seekable streams).  Unknown-
        // length content is buffered; if it turns out to exceed
        // the cap AND is non-seekable, throws immediately.
        await BufferNonSeekableContentAsync(
            originalRequest, cancellationToken)
            .ConfigureAwait(false);

        var host = originalRequest.RequestUri?.Authority ??
                    throw new ArgumentException("originalRequest.RequestUri or originalRequest.RequestUri.Authority property is null.", nameof(originalRequest));
        var requestAttempt1 = await originalRequest.CloneAsync(rewindContent: false, cancellationToken).ConfigureAwait(false);
        var attemptedKey = string.Empty;
        var attachedCachedToken = false;

        // attempt to send request with cached auth token
        if (Cache.TryGetScheme(host, out var schemeFromCache, partitionId))
        {
            switch (schemeFromCache)
            {
                case Challenge.Scheme.Basic:
                    {
                        if (Cache.TryGetToken(host, schemeFromCache, string.Empty, out var basicToken, partitionId))
                        {
                            requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                            // Not flagged as a recoverable cached token: a credential-free re-derive can't
                            // help Basic auth (the same credentials are simply re-sent). Recovery targets
                            // stale Bearer tokens.
                        }

                        break;
                    }
                case Challenge.Scheme.Bearer:
                    {
                        var scopes = ScopeManager.GetScopesStringForHost(host);
                        attemptedKey = string.Join(" ", scopes);
                        if (Cache.TryGetToken(host, schemeFromCache, attemptedKey, out var bearerToken, partitionId))
                        {
                            requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                            attachedCachedToken = true;
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

        // Resolve the 401 through the standard challenge flow. This never throws for a structurally
        // unusable challenge (it reports the reason instead); it DOES throw for credential/token
        // failures, so those propagate and are never eligible for recovery.
        StandardAuthResult resolution;
        try
        {
            resolution = await ResolveStandardChallengeAsync(
                originalRequest: originalRequest,
                unauthorizedResponse: response1,
                host: host,
                partitionId: partitionId,
                attemptedKey: attemptedKey,
                allowAutoRedirect: allowAutoRedirect,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            response1.Dispose();
            throw;
        }

        if (resolution.Response != null)
        {
            // ResolveStandardChallengeAsync disposed the original 401 before the token round-trip.
            return resolution.Response;
        }

        // Standard resolution dead-ended on a challenge it could not use. Consult the optional recovery
        // handler; it decides whether the failure is recoverable (e.g. a stale-token rejection) and, if
        // so, returns a response to continue from. Conformant registries never reach here.
        if (ChallengeRecovery != null)
        {
            HttpResponseMessage? recovered;
            try
            {
                recovered = await InvokeChallengeRecoveryAsync(
                    originalRequest: originalRequest,
                    unauthorizedResponse: response1,
                    host: host,
                    partitionId: partitionId,
                    attemptedKey: attemptedKey,
                    attachedCachedToken: attachedCachedToken,
                    allowAutoRedirect: allowAutoRedirect,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                response1.Dispose();
                throw;
            }

            if (recovered != null)
            {
                response1.Dispose();
                return recovered;
            }
        }

        // No recovery configured, or recovery gave up: preserve the original behavior for this 401.
        return HandleUnusableChallenge(
            unauthorizedResponse: response1,
            resolution: resolution,
            host: host);
    }

    /// <summary>
    /// Runs the standard 401 challenge resolution: parse the <c>WWW-Authenticate</c> challenge and,
    /// for a recognized scheme, fetch/replay credentials. Returns a resolved response on success, or a
    /// <see cref="StandardAuthResult"/> describing why the challenge was structurally unusable. Throws
    /// only for genuine credential/token-endpoint failures (never for an unusable challenge), so a
    /// caller can distinguish "the registry withheld a usable challenge" from "authentication failed".
    /// </summary>
    /// <remarks>
    /// Disposal: on a <em>resolved</em> return this method disposes <paramref name="unauthorizedResponse"/>
    /// before the credential/token round-trip (matching the client's long-standing eager-dispose
    /// behavior); on an <em>unusable</em> return it leaves the response undisposed for the caller to
    /// reuse (recovery) or return/dispose.
    /// </remarks>
    /// <param name="refreshAttemptedScopeKey">
    /// When <c>true</c> (the recovery re-derive), the resolving token is also cached under
    /// <paramref name="attemptedKey"/> so a stale token that provoked recovery is replaced and future
    /// requests under that scope key don't re-enter recovery.
    /// </param>
    private async Task<StandardAuthResult> ResolveStandardChallengeAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage unauthorizedResponse,
        string host,
        string? partitionId,
        string attemptedKey,
        bool allowAutoRedirect,
        CancellationToken cancellationToken,
        bool refreshAttemptedScopeKey = false)
    {
        var (schemeFromChallenge, parameters) =
            Challenge.ParseChallenge(unauthorizedResponse.Headers.WwwAuthenticate.FirstOrDefault()?.ToString());

        // Attempt again with credentials for recognized schemes
        switch (schemeFromChallenge)
        {
            case Challenge.Scheme.Basic:
                {
                    // Usable challenge: release the original 401 before the credential round-trip.
                    unauthorizedResponse.Dispose();
                    var basicAuthToken = await FetchBasicAuthAsync(host, cancellationToken).ConfigureAwait(false);
                    Cache.SetCache(host, schemeFromChallenge, string.Empty, basicAuthToken, partitionId);

                    // Attempt again with basic token
                    var requestAttempt2 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                    requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
                    var basicResponse = await SendRequestAsync(requestAttempt2, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
                    return StandardAuthResult.Resolved(basicResponse);
                }
            case Challenge.Scheme.Bearer:
                {
                    // Usable recognized scheme: release the original 401 up front (matching the client's
                    // long-standing eager dispose). Its status and headers remain readable afterward for
                    // the recovery path.
                    unauthorizedResponse.Dispose();

                    if (parameters == null)
                    {
                        return StandardAuthResult.Unusable(ChallengeFailureKind.MissingParameters);
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
                        Cache.TryGetToken(host, schemeFromChallenge, newKey, out var cachedToken, partitionId))
                    {
                        var requestAttempt2 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                        requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
                        var response2 = await SendRequestAsync(requestAttempt2, allowAutoRedirect, cancellationToken).ConfigureAwait(false);

                        if (response2.StatusCode != HttpStatusCode.Unauthorized)
                        {
                            RefreshAttemptedScopeKeyIfNeeded(
                                refresh: refreshAttemptedScopeKey,
                                resolvedStatus: response2.StatusCode,
                                host: host,
                                scheme: schemeFromChallenge,
                                attemptedKey: attemptedKey,
                                newKey: newKey,
                                token: cachedToken,
                                partitionId: partitionId);
                            return StandardAuthResult.Resolved(response2);
                        }
                        response2.Dispose();
                    }

                    if (!parameters.TryGetValue("realm", out var realm))
                    {
                        // 'realm' is required as it specifies the token endpoint URL for Bearer auth.
                        return StandardAuthResult.Unusable(ChallengeFailureKind.MissingRealm);
                    }

                    // Validate realm URL before sending credentials.
                    if (!Uri.TryCreate(realm, UriKind.Absolute, out var realmUri))
                    {
                        return StandardAuthResult.Unusable(ChallengeFailureKind.InvalidRealm, realm);
                    }

                    var registryUri = originalRequest.RequestUri!;
                    if (!await RealmValidator.IsRealmAllowedAsync(registryUri, realmUri, cancellationToken).ConfigureAwait(false))
                    {
                        return StandardAuthResult.Unusable(ChallengeFailureKind.DeniedRealm, realm, realmUri);
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
                        forceRefresh: true,
                        cancellationToken
                    ).ConfigureAwait(false);
                    Cache.SetCache(host, schemeFromChallenge, newKey, bearerAuthToken, partitionId);

                    var requestAttempt3 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                    requestAttempt3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAuthToken);
                    var bearerResponse = await SendRequestAsync(requestAttempt3, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
                    RefreshAttemptedScopeKeyIfNeeded(
                        refresh: refreshAttemptedScopeKey,
                        resolvedStatus: bearerResponse.StatusCode,
                        host: host,
                        scheme: schemeFromChallenge,
                        attemptedKey: attemptedKey,
                        newKey: newKey,
                        token: bearerAuthToken,
                        partitionId: partitionId);
                    return StandardAuthResult.Resolved(bearerResponse);
                }
            default:
                return StandardAuthResult.Unusable(ChallengeFailureKind.NoUsableScheme);
        }
    }

    /// <summary>
    /// Invokes the configured <see cref="ChallengeRecovery"/> handler for an unusable 401 and continues
    /// from whatever it returns: a fresh 401 is re-run through <see cref="ResolveStandardChallengeAsync"/>
    /// exactly once (using this client's own collaborators); a success or redirect (2xx/3xx) is returned
    /// as-is (e.g. anonymous access, or a captured blob-location redirect); any other status is discarded
    /// so it does not mask the original 401. Returns the
    /// response to continue from, or <c>null</c> to give up. The cold probe carries no token, so a
    /// recovered 401 cannot re-enter recovery — bounded to one pass.
    /// </summary>
    private async Task<HttpResponseMessage?> InvokeChallengeRecoveryAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage unauthorizedResponse,
        string host,
        string? partitionId,
        string attemptedKey,
        bool attachedCachedToken,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        var context = new FailedChallenge(
            statusCode: unauthorizedResponse.StatusCode,
            wwwAuthenticateChallenges: unauthorizedResponse.Headers.WwwAuthenticate.Select(h => h.ToString()).ToArray(),
            host: host,
            attachedCachedToken: attachedCachedToken,
            canReplay: CanReplayWithoutAuthorization(originalRequest),
            probe: ct => ProbeWithoutAuthorizationAsync(
                originalRequest: originalRequest,
                allowAutoRedirect: allowAutoRedirect,
                cancellationToken: ct));

        var recovered = await ChallengeRecovery!(context, cancellationToken).ConfigureAwait(false);
        if (recovered == null)
        {
            return null;
        }

        if (recovered.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The recovery produced a fresh challenge. Re-derive auth from it once, with our own
            // collaborators, and refresh the stale scope key so we don't re-enter recovery next time.
            StandardAuthResult retry;
            try
            {
                retry = await ResolveStandardChallengeAsync(
                    originalRequest: originalRequest,
                    unauthorizedResponse: recovered,
                    host: host,
                    partitionId: partitionId,
                    attemptedKey: attemptedKey,
                    allowAutoRedirect: allowAutoRedirect,
                    cancellationToken: cancellationToken,
                    refreshAttemptedScopeKey: true)
                    .ConfigureAwait(false);
            }
            catch
            {
                recovered.Dispose();
                throw;
            }

            if (retry.Response == null)
            {
                // Still unusable after a cold re-derive — give up; the caller falls back to the 401.
                recovered.Dispose();
                return null;
            }

            // Resolved: ResolveStandardChallengeAsync already disposed the cold-probe 401.
            return retry.Response;
        }

        if (IsSuccessOrRedirect(recovered.StatusCode))
        {
            // The registry served the request without (usable) authorization: a success, or a redirect
            // (e.g. an anonymous blob-location 307 for a caller that captures redirects).
            return recovered;
        }

        // A non-401, non-success/redirect cold response (e.g. 403/404/5xx) is not a recovery; returning it
        // would mask the original 401, which is the more meaningful signal for an auth problem. Give up.
        recovered.Dispose();
        return null;
    }

    /// <summary>
    /// Re-sends <paramref name="originalRequest"/> with no <c>Authorization</c> header to elicit a
    /// fresh challenge from a registry that withheld a usable one for a cached token.
    /// </summary>
    private async Task<HttpResponseMessage> ProbeWithoutAuthorizationAsync(
        HttpRequestMessage originalRequest,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        var probe = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
        probe.Headers.Authorization = null;
        return await SendRequestAsync(probe, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether <paramref name="request"/> can be safely re-sent without authorization — restricted to
    /// idempotent GET/HEAD so a probe never replays a mutating request.
    /// </summary>
    private static bool CanReplayWithoutAuthorization(HttpRequestMessage request)
        => request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;

    /// <summary>Whether <paramref name="statusCode"/> is a 2xx success or a 3xx redirect (200–399).</summary>
    private static bool IsSuccessOrRedirect(HttpStatusCode statusCode)
        => (int)statusCode is >= 200 and < 400;

    /// <summary>
    /// Recovery only: once a token has demonstrably worked (a 2xx/3xx final response), replace the stale
    /// token cached under the originally attempted scope key so future requests under that key succeed
    /// directly instead of re-entering recovery. No-op unless the key actually changed.
    /// </summary>
    private void RefreshAttemptedScopeKeyIfNeeded(
        bool refresh,
        HttpStatusCode resolvedStatus,
        string host,
        Challenge.Scheme scheme,
        string attemptedKey,
        string newKey,
        string token,
        string? partitionId)
    {
        if (refresh
            && IsSuccessOrRedirect(resolvedStatus)
            && !string.Equals(newKey, attemptedKey, StringComparison.Ordinal))
        {
            Cache.SetCache(host, scheme, attemptedKey, token, partitionId);
        }
    }

    /// <summary>
    /// Reproduces the client's default behavior for a structurally unusable challenge when no recovery
    /// applies: an unrecognized scheme yields the original 401; a malformed Bearer challenge throws the
    /// same exception the standard flow has always thrown.
    /// </summary>
    private static HttpResponseMessage HandleUnusableChallenge(
        HttpResponseMessage unauthorizedResponse, StandardAuthResult resolution, string host)
    {
        switch (resolution.FailureKind)
        {
            case ChallengeFailureKind.MissingParameters:
                unauthorizedResponse.Dispose();
                throw new AuthenticationException("Missing parameters in the Www-Authenticate challenge.");
            case ChallengeFailureKind.MissingRealm:
                unauthorizedResponse.Dispose();
                throw new KeyNotFoundException("Missing 'realm' parameter in WWW-Authenticate Bearer challenge.");
            case ChallengeFailureKind.InvalidRealm:
                unauthorizedResponse.Dispose();
                throw new AuthenticationException($"Invalid realm URL: '{resolution.Realm}'");
            case ChallengeFailureKind.DeniedRealm:
                unauthorizedResponse.Dispose();
                throw new AuthenticationException(
                    $"Authentication realm '{resolution.RealmUri}' is not allowed for registry '{host}'.");
            default:
                // NoUsableScheme: no recognized challenge scheme; return the original 401 to the caller.
                return unauthorizedResponse;
        }
    }

    /// <summary>
    /// The outcome of <see cref="ResolveStandardChallengeAsync"/>: either a resolved response, or the
    /// reason the challenge could not be used.
    /// </summary>
    private sealed class StandardAuthResult
    {
        private StandardAuthResult(HttpResponseMessage? response, ChallengeFailureKind failureKind, string? realm, Uri? realmUri)
        {
            Response = response;
            FailureKind = failureKind;
            Realm = realm;
            RealmUri = realmUri;
        }

        /// <summary>The resolved response, or <c>null</c> when the challenge was unusable.</summary>
        public HttpResponseMessage? Response { get; }

        /// <summary>Why the challenge was unusable; <see cref="ChallengeFailureKind.None"/> when resolved.</summary>
        public ChallengeFailureKind FailureKind { get; }

        /// <summary>The raw realm value, when relevant to the failure.</summary>
        public string? Realm { get; }

        /// <summary>The parsed realm URI, when relevant to the failure.</summary>
        public Uri? RealmUri { get; }

        public static StandardAuthResult Resolved(HttpResponseMessage response)
            => new(response, ChallengeFailureKind.None, null, null);

        public static StandardAuthResult Unusable(ChallengeFailureKind failureKind, string? realm = null, Uri? realmUri = null)
            => new(null, failureKind, realm, realmUri);
    }

    /// <summary>
    /// The reason the standard flow could not use a 401's challenge.
    /// </summary>
    private enum ChallengeFailureKind
    {
        /// <summary>The challenge was resolved; not a failure.</summary>
        None,

        /// <summary>No recognized authentication scheme in the challenge (e.g. no challenge at all).</summary>
        NoUsableScheme,

        /// <summary>A Bearer challenge with no parameters.</summary>
        MissingParameters,

        /// <summary>A Bearer challenge missing the required <c>realm</c> parameter.</summary>
        MissingRealm,

        /// <summary>A Bearer challenge whose <c>realm</c> is not an absolute URI.</summary>
        InvalidRealm,

        /// <summary>A Bearer challenge whose <c>realm</c> was rejected by the realm validator.</summary>
        DeniedRealm,
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

    /// <summary>
    /// Buffers non-seekable request content in place so that auth
    /// retries can rewind the body.  Uses the built-in
    /// <see cref="HttpContent.LoadIntoBufferAsync(long)"/> which
    /// handles both known and unknown Content-Length, enforces the
    /// size cap, and makes the stream seekable after completion.
    /// Seekable content that exceeds the buffer size is left
    /// untouched (it can be rewound directly without buffering).
    /// Non-seekable content with a declared Content-Length over the
    /// limit is also left untouched — the first send proceeds but
    /// a 401 retry would fail because the body cannot be replayed.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpContent.LoadIntoBufferAsync(long)"/> on
    /// .NET 8 does not accept a <see cref="CancellationToken"/>;
    /// the token is only used in the catch-path stream check.
    /// With the 4 MB cap, buffering completes quickly in practice.
    /// </remarks>
    private static async Task BufferNonSeekableContentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content;
        if (content == null)
        {
            return;
        }

        // If Content-Length is declared and exceeds the cap, skip
        // buffering to avoid partially consuming a non-seekable
        // stream.  Seekable streams will still work (rewind on
        // retry); non-seekable ones accept the limitation.
        if (content.Headers.ContentLength > MaxBufferSize)
        {
            return;
        }

        try
        {
            await content.LoadIntoBufferAsync(MaxBufferSize)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Content exceeds MaxBufferSize (unknown length that
            // turned out to be too large).  If the stream is
            // seekable, rewind and continue — the retry path can
            // replay the body directly.  If not seekable, the
            // stream is partially consumed and unrecoverable;
            // rethrow so the caller gets a clear size error rather
            // than a confusing "stream already consumed" later.
            var stream = await content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!stream.CanSeek)
            {
                throw;
            }

            stream.Position = 0;
        }
    }
}
