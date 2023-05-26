using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Oras.Remote
{
    /// <summary>
    /// CustomHttpBasicAuthClient adds the Basic Auth Scheme to the Authorization Header
    /// </summary>
    public class CustomHttpBasicAuthClient : HttpClient
    {
        public CustomHttpBasicAuthClient(string username, string password)
        {
            this.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        public CustomHttpBasicAuthClient(string username, string password, HttpMessageHandler handler) : base(handler)
        {
            this.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

    }
}
