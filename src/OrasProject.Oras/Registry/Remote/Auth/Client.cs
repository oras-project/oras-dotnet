using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

public class Client : HttpClient
{
    public required ICredentialHelper Credential { get; set; }
    
    // public HttpRequestHeader Headers { get; set; }
    
    // cache access token
    // basic auth and bearer auth 
    internal Cache Cache { get; set; }
    
    private ScopeManager _scopeManager { get; set; }
    
    public string? ClientId { get; set; }
    
    public bool ForceAttemptOAuth2 { get; set; }
    
    private const string _userAgent = "oras-dotnet";
    
    // constructors
    public Client(ICredentialHelper credential)
    {
        this.AddUserAgent();
        Credential = credential;
    }

    public Client(ICredentialHelper credential, HttpMessageHandler handler) : base(handler)
    {
        this.AddUserAgent();
        Credential = credential;
    }
    
    internal class TokenResponse
    {
        public string? Token { get; set; }
        public string? AccessToken { get; set; }
    }
    
    // do
    public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization != null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var schemeFromCache = Cache.GetScheme(request.RequestUri?.Host);

        switch (schemeFromCache)
        {
            case Challenge.Scheme.Basic:
            {
                var token = Cache.GetToken(request.RequestUri?.Host, Challenge.Scheme.Basic);
                if (token != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                }
                break;
            }
            case Challenge.Scheme.Bearer:
            {
                var scopes = ScopeManager.Instance.GetAllScopesForHost(request.RequestUri?.Host);
                // ScopeManager.Scopes.GetValueOrDefault("nginx");
                var token = Cache.GetToken(request.RequestUri?.Host, Challenge.Scheme.Bearer, scopes);
                if (token != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;
            }
            default:
                break;
        }
        
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }
        
        var (schemeFromChallenge, parameters) = Challenge.ParseChallenge(response.Headers.WwwAuthenticate.ToString());

        if (parameters == null)
        {
            return response;
        }
        
        switch (schemeFromChallenge)
        {
            case Challenge.Scheme.Basic:
            {
                var token = await FetchBasicAuth(request.RequestUri?.Host, cancellationToken).ConfigureAwait(false);
                Cache.SetToken(request.RequestUri?.Host, token, schemeFromChallenge);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                break;
            }
            case Challenge.Scheme.Bearer:
            {
                
                var scopes = ScopeManager.Instance.GetAllScopesForHost(request.RequestUri?.Host);
                // scope hints TODO

                if (parameters.TryGetValue("scope", out var scope))
                {
                    // append scopes   
                }

                // if (key != attemptedKey)
                // {
                //     // ?
                // }

                if (!parameters.ContainsKey("realm"))
                {
                    throw new KeyNotFoundException("Realm was not present in the request.");
                }
                
                if (!parameters.ContainsKey("service"))
                {
                    throw new KeyNotFoundException("Service was not present in the request.");
                }
                
                var token = await FetchBearerAuth(
                    request.RequestUri?.Host, 
                    parameters["realm"], 
                    parameters["service"],
                    scopes,
                    cancellationToken
                ).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
            }
            default:
                return response;
        }
        
        // rewind request
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> FetchBasicAuth(string registry, CancellationToken cancellationToken)
    {
        var credential = await Credential.Resolve(registry, cancellationToken).ConfigureAwait(false);
        if (credential == null)
        {
            throw new Exception("Credentials are missing");
        }

        if (string.IsNullOrEmpty(credential.Username) || string.IsNullOrEmpty(credential.Password))
        {
            throw new Exception("missing username or password for basic auth");
        }
        
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.Username + ":" + credential.Password));
    }

    private async Task<string> FetchBearerAuth(
        string registry, 
        string realm, 
        string service, 
        IList<string> scopes,
        CancellationToken cancellationToken)
    {
        var credential = await Credential.Resolve(registry, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(credential.AccessToken))
        {
            return credential.AccessToken;
        }

        if (IsCredentialEmpty(credential) ||
            (string.IsNullOrEmpty(credential.RefreshToken) && !ForceAttemptOAuth2))
        {
            return await FetchDistributionToken(realm, service, scopes, credential.Username, credential.Password, cancellationToken).ConfigureAwait(false);
        }

        return await FetchOauth2Token(realm, service, scopes, credential, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<string> FetchDistributionToken(
        string realm, 
        string service, 
        IList<string> scopes, 
        string? username, 
        string? password,
        CancellationToken cancellationToken
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, realm);
        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        if (!string.IsNullOrEmpty(service))
        {
            request.Headers.Add("service", service);
        }

        foreach (var scope in scopes)
        {
            request.Headers.Add("scope", scope);
        }
        
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody);

        if (!string.IsNullOrEmpty(tokenResponse?.AccessToken))
        {
            return tokenResponse.AccessToken;
        }

        if (!string.IsNullOrEmpty(tokenResponse?.Token))
        {
            return tokenResponse.Token;
        }

        throw new AuthenticationException("Both AccessToken and Token are empty or missing");
    }

    private async Task<string> FetchOauth2Token(
        string realm, 
        string service,
        IList<string> scopes,
        Credential credential,
        CancellationToken cancellationToken
    )
    {
        var form = new Dictionary<string, string>();
        
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
        
        form["service"] = service;
        form["client_id"] = string.IsNullOrEmpty(ClientId) ? _userAgent : ClientId;

        if (scopes.Count > 0)
        {
            form["scope"] = string.Join(" ", scopes);
        }
        
        var content = new FormUrlEncodedContent(form);
        var request = new HttpRequestMessage(HttpMethod.Post, realm)
        {
            Content = content,
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
        {
            throw new AuthenticationException("AccessToken is empty or missing");
        }
        
        return tokenResponse.AccessToken;
    }
    
    private bool IsCredentialEmpty(Credential credential)
    {
        return string.IsNullOrEmpty(credential.Username) && 
               string.IsNullOrEmpty(credential.Password) && 
               string.IsNullOrEmpty(credential.RefreshToken) && 
               string.IsNullOrEmpty(credential.AccessToken);
    }
}
