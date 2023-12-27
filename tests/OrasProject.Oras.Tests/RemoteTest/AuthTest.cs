using Moq;
using Moq.Protected;
using OrasProject.Oras.Remote.Auth;
using System.Net;
using System.Text;
using Xunit;

namespace OrasProject.Oras.Tests.RemoteTest
{
    public class AuthTest
    {

        public static HttpClient CustomClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func, string username, string password)
        {
            var moqHandler = new Mock<DelegatingHandler>();
            moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            ).ReturnsAsync(func);
            return new HttpClientWithBasicAuth(username, password, moqHandler.Object);
        }

        /// <summary>
        /// TestClient_CustomHttpBasicAuthClient tests the CustomHttpBasicAuthClient class.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestClient_CustomHttpBasicAuthClient()
        {
            var username = "test_user";
            var password = "test_password";
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage
                {
                    RequestMessage = req
                };

                if (req.Method != HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/")
                {
                    res.StatusCode = HttpStatusCode.NotFound;
                    return res;
                }

                var authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                if (req.Headers.Authorization?.ToString() != authHeader)
                {
                    res.Headers.Add("WWW-Authenticate", "Basic realm=\"test\"");
                    res.StatusCode = HttpStatusCode.Unauthorized;
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            };
            var client = CustomClient(func, username, password);
            var response = await client.GetAsync("http://localhost:5000");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
