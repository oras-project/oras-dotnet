using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Linq;

namespace Oras.Remote
{
    internal class RegistryMessageHandler : HttpClientHandler
    {

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = await base.SendAsync(request, cancellationToken);

            // If this is unauthorized there should be a challenge header
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                var token = await GetAccessTokenAsync(res, cancellationToken);
                request.Headers.Add("Authorization", "Bearer " + token);
                return await base.SendAsync(request, cancellationToken);
            }

            return res;
        }
    
        public const string AuthenticateHeaderKey = "Www-Authenticate";

        /// <summary>
        /// Get a docker access token for a URI using OAuth2 flow with retry.
        /// </summary>
        public async Task<string> GetAccessTokenAsync(
            HttpResponseMessage challenge,
            CancellationToken cancellationToken)
        {
            var uri = challenge.RequestMessage?.RequestUri;

            /* 
             * Www-Authenticate: Bearer realm="https://auth.docker.io/token",
             * service="registry.docker.io",scope="repository:library/official-app:pull"
             */
            if (challenge.StatusCode != HttpStatusCode.Unauthorized
                || !challenge.Headers.Contains(AuthenticateHeaderKey))
            {
                throw new Exception($"URI {uri} did not issue a challenge with status code: {challenge.StatusCode}");
            }

            var authenticateHeaderValue = challenge.Headers.GetValues(AuthenticateHeaderKey).FirstOrDefault();

            if (string.IsNullOrEmpty(authenticateHeaderValue))
            {
                throw new Exception($"Empty authenticate header.");
            }

            var authenticate = authenticateHeaderValue.Split(' ');
            if (authenticate.Length != 2 || string.Compare(authenticate[0], "Bearer", true) < 0)
            {
                throw new Exception($"URI {uri} did not return correct authenticate header {authenticateHeaderValue}.");
            }

            var tokens = authenticate[1].Split(',').Select(t =>
            {
                return t.Trim().Split('=');
            }).ToDictionary(t => t[0], t => t[1]);

            if (!(tokens.ContainsKey("realm")
                && tokens.ContainsKey("service")
                && tokens.ContainsKey("scope")))
            {
                throw new Exception($"URI {uri} did not return authenticate header with necessary fields {authenticateHeaderValue}.");
            }

            var authUri = $"{tokens["realm"].Trim('"')}?service={tokens["service"].Trim('"')}&scope={tokens["scope"].Trim('"')}";

            // handle retries
            // create request message for authUri
            var requestMsg = new HttpRequestMessage(HttpMethod.Get, authUri);
            var response = await base.SendAsync(requestMsg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var strToken = await response?.Content?.ReadAsStringAsync();

                var oAuthToken = JsonSerializer.Deserialize<OAuthToken>(strToken);
                if (string.IsNullOrEmpty(oAuthToken?.Token))
                {
                    throw new Exception($"URI {authUri} could not return a valid access token.");
                }

                return oAuthToken.Token;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new Exception($"Request failed with status code: {response.StatusCode}.");
            }
            else
            {
                throw new Exception($"Request failed with status code: {response.StatusCode}.");
            }
        }
    }

    class OAuthToken
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("issued_at")]
        public string IssuedAt { get; set; }
    }
}
