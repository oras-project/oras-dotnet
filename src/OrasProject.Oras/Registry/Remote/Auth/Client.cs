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

public class Client(HttpClient? httpClient = null, ICredentialHelper? credentialHelper = null)
    : IClient
{
    /// <summary>
    /// CredentialHelper provides the mechanism to retrieve
    /// credentials for accessing remote registries.
    /// </summary>
    public ICredentialHelper? CredentialHelper { get; } = credentialHelper;

    /// <summary>
    /// BaseClient is an instance of HttpClient to send http requests
    /// </summary>
    public HttpClient BaseClient { get; } = httpClient ?? DefaultHttpClient.Instance;

    /// <summary>
    /// Cache used for storing and retrieving 
    /// authentication-related data to optimize remote registry operations.
    /// </summary>
    public ICache Cache { get; set; } = new Cache();
    
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
    public ConcurrentDictionary<string, List<string>> CustomHeaders { get; set; } = new();

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
    /// SendAsync sends an HTTP request asynchronously, attempting to resolve authentication if 'Authorization' header is not set.
    /// 
    /// This method intercepts the HTTP request to handle authentication challenges, including Basic and Bearer schemes.
    /// It attempts to retrieve cached tokens for the request's host and applies them to the request headers.
    /// If the server responds with an unauthorized status, it parses the authentication challenge and fetches
    /// new tokens as needed, updating the cache accordingly.
    /// </summary>
    /// <param name="originalRequest">The original HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP response message.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if the request URI is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if required parameters (e.g., "realm" or "service") are missing in the authentication challenge.
    /// </exception>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage originalRequest,
        CancellationToken cancellationToken)
    {
        foreach (var (headerName, headerValues) in CustomHeaders)
        {
            originalRequest.Headers.TryAddWithoutValidation(headerName, headerValues);
        }
        
        originalRequest.AddDefaultUserAgent();
        if (originalRequest.Headers.Authorization != null || BaseClient.DefaultRequestHeaders.Authorization != null)
        {
            return await BaseClient.SendAsync(originalRequest, cancellationToken).ConfigureAwait(false);
        }
        var host = originalRequest.RequestUri?.Host ?? throw new ArgumentNullException(nameof(originalRequest.RequestUri));
        using var requestAttempt1 = await originalRequest.CloneAsync(cancellationToken).ConfigureAwait(false);
        var attemptedKey = string.Empty;

        // attempt to send request with cached auth token
        if (Cache.TryGetScheme(host, out var schemeFromCache))
        {
            switch (schemeFromCache)
            {
                case Challenge.Scheme.Basic:
                {
                    if (Cache.TryGetToken(host, schemeFromCache, string.Empty, out var basicToken))
                    {
                        requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                    }

                    break;
                }
                case Challenge.Scheme.Bearer:
                {
                    var scopes = ScopeManager.GetScopesStringForHost(host);
                    attemptedKey = string.Join(" ", scopes);
                    if (Cache.TryGetToken(host, schemeFromCache, attemptedKey, out var bearerToken))
                    {
                        requestAttempt1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    }

                    break;
                }
            }
        }

        var response1 = await BaseClient.SendAsync(requestAttempt1, cancellationToken).ConfigureAwait(false);
        if (response1.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response1;
        }

        var (schemeFromChallenge, parameters) =
            Challenge.ParseChallenge(response1.Headers.WwwAuthenticate.FirstOrDefault()?.ToString());

        // attempt again with credentials for recognized schemes
        switch (schemeFromChallenge)
        {
            case Challenge.Scheme.Basic:
            {
                response1.Dispose();
                var basicAuthToken = await FetchBasicAuth(host, cancellationToken).ConfigureAwait(false);
                Cache.SetCache(host, schemeFromChallenge, string.Empty, basicAuthToken);
                
                // Attempt again with basic token
                using var requestAttempt2 = await originalRequest.CloneAsync(cancellationToken).ConfigureAwait(false);
                requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
                return await BaseClient.SendAsync(requestAttempt2, cancellationToken).ConfigureAwait(false);
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

                // attempt to send request when the scope changes and a token cache hits
                var newKey = string.Join(" ", newScopes.Select(newScope => newScope));
                if (newKey != attemptedKey &&
                    Cache.TryGetToken(host, schemeFromChallenge, newKey, out var cachedToken))
                {
                    using var requestAttempt2 = await originalRequest.CloneAsync(cancellationToken).ConfigureAwait(false);
                    requestAttempt2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
                    var response2 = await BaseClient.SendAsync(requestAttempt2, cancellationToken).ConfigureAwait(false);
                    
                    if (response2.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        return response2;
                    }
                    response2.Dispose();
                }

                if (!parameters.TryGetValue("realm", out var realm))
                {
                    throw new KeyNotFoundException("Realm was not present in the request.");
                }

                if (!parameters.TryGetValue("service", out var service))
                {
                    throw new KeyNotFoundException("Service was not present in the request.");
                }

                // try to fetch bearer token based on the challenge header
                var bearerAuthToken = await FetchBearerAuth(
                    host,
                    realm,
                    service,
                    newScopes.Select(newScope => newScope.ToString()).ToList(),
                    cancellationToken
                ).ConfigureAwait(false);
                Cache.SetCache(host, schemeFromChallenge, newKey, bearerAuthToken);

                using var requestAttempt3 = await originalRequest.CloneAsync(cancellationToken).ConfigureAwait(false);
                requestAttempt3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAuthToken);
                return await BaseClient.SendAsync(requestAttempt3, cancellationToken).ConfigureAwait(false);
            }
            default:
                return response1;
        }
    }

    /// <summary>
    /// Fetches the Basic Authentication token for the specified registry.
    /// </summary>
    /// <param name="registry">The registry for which the credentials are being fetched.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A Base64-encoded string representing the Basic Authentication token in the format "username:password".
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or when the username or password is null or empty.
    /// </exception>
    internal async Task<string> FetchBasicAuth(string registry, CancellationToken cancellationToken)
    {
        if (CredentialHelper == null)
        {
            throw new AuthenticationException("CredentialHelper is not configured");
        }

        var credential = await CredentialHelper.ResolveAsync(registry, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrWhiteSpace(credential.Password))
        {
            throw new AuthenticationException("Missing username or password for basic authentication.");
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"));
    }

    /// <summary>
    /// Fetches a OAuth2 access token for accessing a registry.
    ///
    /// If credential is empty or refreshToken is not set and OAuth2 authentication is not forced,
    /// the method would fetch anonymous token with a Http Get request
    /// otherwise, it would fetch OAuth2 token with a Http Post request.
    /// </summary>
    /// <param name="registry">The registry URL or identifier.</param>
    /// <param name="realm">The authentication realm to use.</param>
    /// <param name="service">The service name for which the token is requested.</param>
    /// <param name="scopes">The scopes of access requested for the token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the bearer authentication token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">Thrown when credentials are missing or invalid.</exception>
    internal async Task<string> FetchBearerAuth(
        string registry, 
        string realm, 
        string service, 
        IList<string> scopes,
        CancellationToken cancellationToken)
    {
        var credential = await (CredentialHelper?.ResolveAsync(registry, cancellationToken)
                                ?? Task.FromResult<Credential>(new Credential()))
            .ConfigureAwait(false);
        
        if (!string.IsNullOrEmpty(credential.AccessToken))
        {
            return credential.AccessToken;
        }

        if (credential.IsEmpty() || 
            (string.IsNullOrWhiteSpace(credential.RefreshToken) && !ForceAttemptOAuth2))
        {
            return await FetchDistributionToken(
                realm, 
                service, 
                scopes, 
                credential.Username, 
                credential.Password, 
                cancellationToken
            ).ConfigureAwait(false);
        }

        return await FetchOauth2Token(
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
    internal async Task<string> FetchDistributionToken(
        string realm, 
        string service, 
        IList<string> scopes, 
        string? username, 
        string? password,
        CancellationToken cancellationToken
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
        
        using var response = await BaseClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
    internal async Task<string> FetchOauth2Token(
        string realm, 
        string service,
        IList<string> scopes,
        Credential credential,
        CancellationToken cancellationToken
    )
    {
        var form = new Dictionary<string, string>
        {
            ["service"] = service,
            ["client_id"] = string.IsNullOrEmpty(ClientId) ? _defaultClientId : ClientId
        };
        
        if (!string.IsNullOrEmpty(credential.RefreshToken))
        {
            form["grant_type"] = "refresh_token";
            form["refresh_token"] = credential.RefreshToken;
        } else if (!string.IsNullOrEmpty(credential.Username) && !string.IsNullOrEmpty(credential.Password))
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
        
        using var response = await BaseClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
}
