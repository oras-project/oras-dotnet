﻿using Moq.Protected;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Oras.Remote;
using System.Text.RegularExpressions;

namespace Oras.Tests.RemoteTest
{
    public class RegistryTest
    {

        public static HttpClient CustomClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
        {
            var moqHandler = new Mock<DelegatingHandler>();
            moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            ).ReturnsAsync(func);
            return new HttpClient(moqHandler.Object);
        }

        /// <summary>
        /// TestRegistry_PingAsync tests the PingAsync method of the Registry class.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestRegistry_PingAsync()
        {
            var V2Implemented = true;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;

                if (req.Method != HttpMethod.Get && req.RequestUri.AbsolutePath == $"/v2/")
                {
                    res.StatusCode = HttpStatusCode.NotFound;
                    return res;
                }

                if (V2Implemented)
                {
                    res.StatusCode = HttpStatusCode.OK;
                    return res;
                }
                else
                {
                    res.StatusCode = HttpStatusCode.NotFound;
                    return res;
                }
            };
            var registry = new Oras.Remote.Registry("localhost:5000");
            registry.PlainHTTP = true;
            registry.HttpClient = CustomClient(func);
            var cancellationToken = new CancellationToken();
            await registry.PingAsync(cancellationToken);
            V2Implemented = false;
            await Assert.ThrowsAnyAsync<Exception>(
                async () => await registry.PingAsync(cancellationToken));
        }

        /// <summary>
        /// TestRegistry_ListRepositoriesAsync tests the ListRepositoriesAsync method of the Registry class.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task TestRegistry_ListRepositoriesAsync()
        {
            var repoSet = new List<List<string>>()
            {
                new() {"the", "quick", "brown", "fox"},
                new() {"jumps", "over", "the", "lazy"},
                new() {"dog"}
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get ||
                    req.RequestUri.AbsolutePath != "/v2/_catalog"
                   )
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                var q = req.RequestUri.Query;
                try
                {
                    var n = int.Parse(Regex.Match(q, @"(?<=n=)\d+").Value);
                    if (n != 4) throw new Exception();
                }
                catch (Exception e)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                var repos = new List<string>();
                var serverUrl = "http://localhost:5000";
                var matched = Regex.Match(q, @"(?<=test=)\w+").Value;
                switch (matched)
                {
                    case "foo":
                        repos = repoSet[1];
                        res.Headers.Add("Link", $"<{serverUrl}/v2/_catalog?n=4&test=bar>; rel=\"next\"");
                        break;
                    case "bar":
                        repos = repoSet[2];
                        break;
                    default:
                        repos = repoSet[0];
                        res.Headers.Add("Link", $"</v2/_catalog?n=4&test=foo>; rel=\"next\"");
                        break;
                }
                res.Content = new StringContent(JsonSerializer.Serialize(repos));
                return res;

            };

            var registry = new Oras.Remote.Registry("localhost:5000");
            registry.PlainHTTP = true;
            registry.HttpClient = CustomClient(func);
            var cancellationToken = new CancellationToken();
            registry.TagListPageSize = 4;

          

            var index = 0;
            await registry.ListRepositoriesAsync("", async (string[] got) =>
            {
                if (index > 2)
                {
                    throw new Exception($"Error out of range: {index}");
                }

                var repos = repoSet[index];
                index++;
                Assert.Equal(got, repos);
            }, cancellationToken);
        }
    }
}
