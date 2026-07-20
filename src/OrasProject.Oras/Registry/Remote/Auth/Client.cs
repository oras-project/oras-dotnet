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
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Exceptions;

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
        // The calls below follow the challenge to its token endpoint (the realm). Scope any 401 from
        // that round-trip into a distinct marker so the standard resolver can offer it to built-in
        // challenge recovery instead of failing outright. A cached out-of-band token from an
        // AccessTokenProvider (handled above) is deliberately excluded because it does not follow the
        // challenge, and non-401 token-endpoint errors propagate unchanged.
        try
        {
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
        catch (ResponseException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TokenEndpointUnauthorizedException(e);
        }
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
        var selectedClient = allowAutoRedirect ? BaseClient : NoRedirectClient;
        if (originalRequest.Headers.Authorization != null
            || selectedClient.DefaultRequestHeaders.Authorization != null)
        {
            return await SendRequestAsync(originalRequest, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
        }

        // Buffer non-seekable content upfront so auth retries can
        // rewind it.  Skips when Content-Length exceeds 4 MB (to
        // avoid consuming large non-seekable streams).  Unknown-
        // length content is buffered; if it turns out to exceed
        // the cap AND is non-seekable, throws immediately.
        var requestContentReplayable = await BufferNonSeekableContentAsync(
            originalRequest, cancellationToken)
            .ConfigureAwait(false);

        var host = originalRequest.RequestUri?.Authority ??
                    throw new ArgumentException("originalRequest.RequestUri or originalRequest.RequestUri.Authority property is null.", nameof(originalRequest));
        var requestAttempt1 = await originalRequest.CloneAsync(rewindContent: false, cancellationToken).ConfigureAwait(false);
        var attemptedKey = string.Empty;
        var attachedCachedBearerToken = false;

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
                        var scopes = ScopeManager.GetScopesStringForHost(host, partitionId);
                        attemptedKey = string.Join(" ", scopes);
                        if (Cache.TryGetToken(host, schemeFromCache, attemptedKey, out var bearerToken, partitionId))
                        {
                            requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                            attachedCachedBearerToken = true;
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
        // failures other than a token-endpoint 401, so those propagate and are never eligible for
        // recovery.
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

        // Standard resolution dead-ended on a challenge it could not use. If the rejected request
        // carried a cached bearer token, re-derive the challenge once without authorization. This
        // fallback runs only after normal resolution fails, so conformant registries and legitimate
        // credentialed flows do not incur an extra request.
        if (attachedCachedBearerToken)
        {
            HttpResponseMessage? recovered;
            try
            {
                recovered = await TryRecoverChallengeAsync(
                    unauthorizedResponseToBuffer:
                        resolution.FailureKind == ChallengeFailureKind.NoUsableScheme
                            && originalRequest.Method != HttpMethod.Head
                            ? response1
                            : null,
                    originalRequest: originalRequest,
                    requestContentReplayable: requestContentReplayable,
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

            if (recovered != null)
            {
                response1.Dispose();
                return recovered;
            }
        }

        // Recovery did not apply or gave up: preserve the original behavior for this 401.
        return HandleUnusableChallenge(
            unauthorizedResponse: response1,
            resolution: resolution,
            host: host);
    }

    /// <summary>
    /// Runs the standard 401 challenge resolution: parse the <c>WWW-Authenticate</c> challenge and,
    /// for a recognized scheme, fetch/replay credentials. Returns a resolved response on success, or a
    /// <see cref="StandardAuthResult"/> describing why the challenge was structurally unusable. A 401
    /// from following the challenge's token endpoint is reported as
    /// <see cref="ChallengeFailureKind.TokenEndpointUnauthorized"/> (carrying the original exception for
    /// verbatim rethrow), not thrown, so recovery can act on it. It still throws for other genuine
    /// credential/token-endpoint failures, so a caller can distinguish "the registry withheld a usable
    /// challenge" from "authentication failed".
    /// </summary>
    /// <remarks>
    /// Disposal: for a recognized scheme (Basic or Bearer) this method disposes
    /// <paramref name="unauthorizedResponse"/> up front, before the credential/token round-trip (matching
    /// the client's long-standing eager-dispose behavior) — whether the outcome is resolved or a
    /// structurally-unusable Bearer challenge. Only the <see cref="ChallengeFailureKind.NoUsableScheme"/>
    /// outcome leaves the response undisposed, so the caller can return it or hand it to the fallback.
    /// </remarks>
    /// <param name="refreshAttemptedScopeKey">
    /// When <c>true</c> (the recovery re-derive, gated on a stale token having been attached), a token
    /// obtained from a successful (2xx/3xx) response is also cached under <paramref name="attemptedKey"/>
    /// so the stale token that provoked recovery is replaced and future requests under that scope key
    /// don't re-enter recovery.
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

                    var existingScopes = ScopeManager.GetScopesForHost(host, partitionId);
                    var mergedScopes = MergeChallengeScopes(
                        existingScopes,
                        parameters.TryGetValue("scope", out var scopesString)
                            ? scopesString
                            : null);

                    // Attempt to send request when the scope changes and a token cache hits
                    if (!mergedScopes.HasOpaqueScopes
                        && mergedScopes.CacheKey != attemptedKey
                        && Cache.TryGetToken(
                            host,
                            schemeFromChallenge,
                            mergedScopes.CacheKey,
                            out var cachedToken,
                            partitionId))
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
                                newKey: mergedScopes.CacheKey,
                                hasOpaqueScopes: mergedScopes.HasOpaqueScopes,
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
                    if (!Uri.TryCreate(
                            realm, UriKind.Absolute,
                            out var realmUri)
                        || string.IsNullOrEmpty(realmUri.Host))
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
                    string bearerAuthToken;
                    try
                    {
                        bearerAuthToken = await FetchBearerAuthAsync(
                            host,
                            realm,
                            service,
                            mergedScopes.TokenRequestScopes,
                            forceRefresh: true,
                            cancellationToken
                        ).ConfigureAwait(false);
                    }
                    catch (TokenEndpointUnauthorizedException e)
                    {
                        // We followed the challenge to its token endpoint and it returned 401. Report it
                        // as unusable so built-in recovery can cold-probe a challenge derived from a stale
                        // cached token. If recovery declines, the original exception is rethrown as-is.
                        return StandardAuthResult.Unusable(
                            ChallengeFailureKind.TokenEndpointUnauthorized,
                            realm,
                            realmUri,
                            ExceptionDispatchInfo.Capture(e.ResponseException));
                    }
                    if (!mergedScopes.HasOpaqueScopes)
                    {
                        Cache.SetCache(
                            host,
                            schemeFromChallenge,
                            mergedScopes.CacheKey,
                            bearerAuthToken,
                            partitionId);
                    }

                    var requestAttempt3 = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
                    requestAttempt3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAuthToken);
                    var bearerResponse = await SendRequestAsync(requestAttempt3, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
                    RefreshAttemptedScopeKeyIfNeeded(
                        refresh: refreshAttemptedScopeKey,
                        resolvedStatus: bearerResponse.StatusCode,
                        host: host,
                        scheme: schemeFromChallenge,
                        attemptedKey: attemptedKey,
                        newKey: mergedScopes.CacheKey,
                        hasOpaqueScopes: mergedScopes.HasOpaqueScopes,
                        token: bearerAuthToken,
                        partitionId: partitionId);
                    return StandardAuthResult.Resolved(bearerResponse);
                }
            default:
                return StandardAuthResult.Unusable(ChallengeFailureKind.NoUsableScheme);
        }
    }

    private static MergedScopes MergeChallengeScopes(
        SortedSet<Scope> existingScopes,
        string? scopesString)
    {
        var structuredScopes = existingScopes;
        var copiedStructuredScopes = false;

        // Opaque (unparseable) challenge scopes are preserved verbatim, including
        // duplicates and original order. The opaque list is allocated lazily so
        // the common structured-only case doesn't allocate it.
        List<string>? opaqueScopes = null;

        if (!string.IsNullOrWhiteSpace(scopesString))
        {
            var remainingScopes = scopesString.AsSpan();
            while (TryReadNextScope(ref remainingScopes, out var scopeSpan))
            {
                if (Scope.TryParse(scopeSpan, out var scope))
                {
                    if (!copiedStructuredScopes)
                    {
                        // Copy before merging so an attacker-controllable challenge scope
                        // cannot mutate the client's persisted scopes for this host.
                        structuredScopes = new SortedSet<Scope>(existingScopes);
                        copiedStructuredScopes = true;
                    }

                    Scope.AddOrMergeScopeCopyOnWrite(structuredScopes, scope);
                }
                else
                {
                    (opaqueScopes ??= new()).Add(scopeSpan.ToString());
                }
            }
        }

        var tokenRequestScopes = BuildTokenRequestScopes(
            structuredScopes,
            opaqueScopes,
            out var cacheKey);
        return new MergedScopes(
            tokenRequestScopes,
            cacheKey,
            hasOpaqueScopes: opaqueScopes != null);
    }

    private static List<string> BuildTokenRequestScopes(
        SortedSet<Scope> structuredScopes,
        List<string>? opaqueScopes,
        out string cacheKey)
    {
        var opaqueScopeCount = opaqueScopes?.Count ?? 0;
        var tokenRequestScopes =
            new List<string>(structuredScopes.Count + opaqueScopeCount);

        foreach (var scope in structuredScopes)
        {
            tokenRequestScopes.Add(scope.ToString());
        }

        // The cache key is derived from structured scopes only and must match the
        // single-space canonicalization used for the cached-token lookup. When opaque
        // scopes are present the token is not cached, so the key is left empty. At this
        // point tokenRequestScopes holds only the structured scopes, so it can be joined
        // directly before the opaque scopes are appended.
        cacheKey = opaqueScopeCount > 0
            ? string.Empty
            : string.Join(' ', tokenRequestScopes);

        if (opaqueScopes != null)
        {
            tokenRequestScopes.AddRange(opaqueScopes);
        }

        return tokenRequestScopes;
    }

    private static bool TryReadNextScope(
        ref ReadOnlySpan<char> remainingScopes,
        out ReadOnlySpan<char> scope)
    {
        while (!remainingScopes.IsEmpty)
        {
            var separatorIndex = remainingScopes.IndexOf(' ');
            scope = separatorIndex < 0
                ? remainingScopes
                : remainingScopes[..separatorIndex];
            remainingScopes = separatorIndex < 0
                ? []
                : remainingScopes[(separatorIndex + 1)..];

            if (!scope.IsEmpty)
            {
                return true;
            }
        }

        scope = [];
        return false;
    }

    private readonly struct MergedScopes
    {
        public MergedScopes(
            List<string> tokenRequestScopes,
            string cacheKey,
            bool hasOpaqueScopes)
        {
            TokenRequestScopes = tokenRequestScopes;
            CacheKey = cacheKey;
            HasOpaqueScopes = hasOpaqueScopes;
        }

        public List<string> TokenRequestScopes { get; }

        public string CacheKey { get; }

        public bool HasOpaqueScopes { get; }
    }

    /// <summary>
    /// Re-derives an unusable challenge without authorization and continues from the cold response. A
    /// fresh 401 is re-run through <see cref="ResolveStandardChallengeAsync"/> exactly once; a success or
    /// redirect (2xx/3xx) is returned as-is; any other status is discarded so it does not mask the
    /// original 401. The cold probe carries no token, so recovery is bounded to one pass.
    /// </summary>
    private async Task<HttpResponseMessage?> TryRecoverChallengeAsync(
        HttpResponseMessage? unauthorizedResponseToBuffer,
        HttpRequestMessage originalRequest,
        bool requestContentReplayable,
        string host,
        string? partitionId,
        string attemptedKey,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        if (!CanReplayWithoutAuthorization(
                originalRequest,
                requestContentReplayable,
                unauthorizedResponseToBuffer))
        {
            return null;
        }

        var recovered = await ProbeWithoutAuthorizationAsync(
            originalRequest: originalRequest,
            unauthorizedResponseToBuffer: unauthorizedResponseToBuffer,
            allowAutoRedirect: allowAutoRedirect,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        recovered.RequestMessage ??= originalRequest;

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
    /// fresh challenge from a registry that withheld a usable one for a cached token. When the
    /// original no-challenge 401 is still alive, buffers its bounded content first so the response
    /// releases its connection before the same-host probe.
    /// </summary>
    private async Task<HttpResponseMessage> ProbeWithoutAuthorizationAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage? unauthorizedResponseToBuffer,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        if (unauthorizedResponseToBuffer != null)
        {
            await BufferResponseContentAsync(
                    unauthorizedResponseToBuffer,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var probe = await originalRequest.CloneAsync(rewindContent: true, cancellationToken).ConfigureAwait(false);
        probe.Headers.Authorization = null;
        return await SendRequestAsync(probe, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether <paramref name="request"/> can be safely re-sent without authorization: the request is
    /// an idempotent GET/HEAD, and any still-live 401 content can be bounded-buffered to release its
    /// connection before the same-host probe.
    /// </summary>
    private static bool CanReplayWithoutAuthorization(
        HttpRequestMessage request,
        bool requestContentReplayable,
        HttpResponseMessage? unauthorizedResponseToBuffer)
    {
        if (!requestContentReplayable
            || (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head))
        {
            return false;
        }

        if (unauthorizedResponseToBuffer == null)
        {
            return true;
        }

        var contentLength = unauthorizedResponseToBuffer.Content.Headers.ContentLength;
        return contentLength.HasValue
            && contentLength.Value <= MaxBufferSize;
    }

    private static async Task BufferResponseContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = response.Content;
        var headers = content.Headers.ToArray();
        byte[] bufferedBytes;

        using (var source = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false))
        using (var destination = new MemoryStream())
        {
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await source
                .ReadAsync(buffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false)) != 0)
            {
                if (destination.Length > MaxBufferSize - bytesRead)
                {
                    throw new HttpRequestException(
                        $"The unauthorized response content exceeds {MaxBufferSize} bytes.");
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
            }

            bufferedBytes = destination.ToArray();
        }

        var bufferedContent = new ByteArrayContent(bufferedBytes);
        foreach (var header in headers)
        {
            bufferedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content = bufferedContent;
        content.Dispose();
    }

    /// <summary>Whether <paramref name="statusCode"/> is a 2xx success or a 3xx redirect (200–399).</summary>
    private static bool IsSuccessOrRedirect(HttpStatusCode statusCode)
        => (int)statusCode is >= 200 and < 400;

    /// <summary>
    /// Once a recovered token has demonstrably worked (a 2xx/3xx final response), replace the stale token
    /// cached under the originally attempted non-empty scope key so future requests under that key succeed
    /// directly instead of re-entering recovery. No-op for opaque scopes or unless the key changed.
    /// </summary>
    private void RefreshAttemptedScopeKeyIfNeeded(
        bool refresh,
        HttpStatusCode resolvedStatus,
        string host,
        Challenge.Scheme scheme,
        string attemptedKey,
        string newKey,
        bool hasOpaqueScopes,
        string token,
        string? partitionId)
    {
        if (refresh
            && !string.IsNullOrEmpty(attemptedKey)
            && IsSuccessOrRedirect(resolvedStatus)
            && !hasOpaqueScopes
            && !string.Equals(newKey, attemptedKey, StringComparison.Ordinal))
        {
            Cache.SetCache(host, scheme, attemptedKey, token, partitionId);
        }
    }

    /// <summary>
    /// Reproduces the client's default behavior unless built-in recovery
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
            case ChallengeFailureKind.TokenEndpointUnauthorized:
                // We followed the challenge to its token endpoint and it returned 401. Recovery declined,
                // so rethrow the exact token-endpoint exception with its original stack.
                unauthorizedResponse.Dispose();
                resolution.TokenEndpointException!.Throw();
                throw resolution.TokenEndpointException.SourceException; // unreachable; Throw() always throws
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
        private StandardAuthResult(
            HttpResponseMessage? response,
            ChallengeFailureKind failureKind,
            string? realm,
            Uri? realmUri,
            ExceptionDispatchInfo? tokenEndpointException)
        {
            Response = response;
            FailureKind = failureKind;
            Realm = realm;
            RealmUri = realmUri;
            TokenEndpointException = tokenEndpointException;
        }

        /// <summary>The resolved response, or <c>null</c> when the challenge was unusable.</summary>
        public HttpResponseMessage? Response { get; }

        /// <summary>Why the challenge was unusable; <see cref="ChallengeFailureKind.None"/> when resolved.</summary>
        public ChallengeFailureKind FailureKind { get; }

        /// <summary>The raw realm value, when relevant to the failure.</summary>
        public string? Realm { get; }

        /// <summary>The parsed realm URI, when relevant to the failure.</summary>
        public Uri? RealmUri { get; }

        /// <summary>
        /// The captured token-endpoint exception for a
        /// <see cref="ChallengeFailureKind.TokenEndpointUnauthorized"/> failure, rethrown verbatim when
        /// recovery does not apply; <c>null</c> for all other outcomes.
        /// </summary>
        public ExceptionDispatchInfo? TokenEndpointException { get; }

        public static StandardAuthResult Resolved(HttpResponseMessage response)
            => new(response, ChallengeFailureKind.None, null, null, null);

        public static StandardAuthResult Unusable(
            ChallengeFailureKind failureKind,
            string? realm = null,
            Uri? realmUri = null,
            ExceptionDispatchInfo? tokenEndpointException = null)
            => new(null, failureKind, realm, realmUri, tokenEndpointException);
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

        /// <summary>
        /// The challenge was structurally usable and its realm was followed, but the token endpoint
        /// rejected the token request with a 401 (e.g. a non-conformant registry that cannot mint a
        /// token for a challenge it derived from a stale cached token). Distinct from the failure kinds
        /// above, which never contacted the token endpoint.
        /// </summary>
        TokenEndpointUnauthorized,
    }

    /// <summary>
    /// Internal marker raised when following a 401's challenge to its token endpoint itself returns a
    /// 401. It carries the original <see cref="Exceptions.ResponseException"/> so the standard resolver
    /// can convert it into a <see cref="ChallengeFailureKind.TokenEndpointUnauthorized"/> result for
    /// built-in recovery while still rethrowing the exact original error when recovery does not apply.
    /// Scoped to the realm round-trip only — an out-of-band AccessTokenProvider 401 is excluded.
    /// </summary>
    private sealed class TokenEndpointUnauthorizedException : Exception
    {
        public TokenEndpointUnauthorizedException(ResponseException responseException)
            : base(responseException.Message, responseException)
        {
            ResponseException = responseException;
        }

        /// <summary>The original token-endpoint 401 exception, rethrown verbatim when recovery declines.</summary>
        public ResponseException ResponseException { get; }
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
    private static async Task<bool> BufferNonSeekableContentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content;
        if (content == null)
        {
            return true;
        }

        // If Content-Length is declared and exceeds the cap, skip
        // buffering to avoid partially consuming a non-seekable
        // stream.  Seekable streams will still work (rewind on
        // retry); non-seekable ones accept the limitation.
        if (content.Headers.ContentLength > MaxBufferSize)
        {
            return false;
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

        return true;
    }
}
