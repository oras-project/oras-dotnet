using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Oras.Remote.Auth
{
    /// <summary>
    /// HttpClientWithBasicAuth adds the Basic Auth Scheme to the Authorization Header
    /// </summary>
    public class HttpClientWithBasicAuth : HttpClient
    {
        public HttpClientWithBasicAuth(string username, string password)
        {
            this.AddUserAgent();
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        public HttpClientWithBasicAuth(string username, string password, HttpMessageHandler handler) : base(handler)
        {
            this.AddUserAgent();
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

    }
}
