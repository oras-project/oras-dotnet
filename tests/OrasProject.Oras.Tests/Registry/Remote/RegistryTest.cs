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

using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using OrasProject.Oras.Registry.Remote.Auth;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote;

public partial class RegistryTest
{
    [GeneratedRegex(@"(?<=n=)\d+")]
    private static partial Regex NQueryParam();

    [GeneratedRegex(@"(?<=test=)\w+")]
    private static partial Regex TestQueryParam();

    /// <summary>
    /// Test registry constructor
    /// </summary>
    [Fact]
    public void Registry()
    {
        var registryName = "foobar";
        var registry = new Oras.Registry.Remote.Registry(registryName);
        var options = registry.RepositoryOptions;
        Assert.Equal(registryName, options.Reference.Registry);
    }

    /// <summary>
    /// PingAsync tests the PingAsync method of the Registry class.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task PingAsync()
    {
        var V2Implemented = true;

        HttpResponseMessage Func(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };

            if (req.Method != HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/")
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
        }
        var registry = new Oras.Registry.Remote.Registry(new RepositoryOptions()
        {
            Reference = new Reference("localhost:5000"),
            PlainHttp = true,
            Client = CustomClient(Func),
        });
        var cancellationToken = new CancellationToken();
        await registry.PingAsync(cancellationToken);
        V2Implemented = false;
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await registry.PingAsync(cancellationToken));
    }

    /// <summary>
    /// Repositories tests the ListRepositoriesAsync method of the Registry class.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [Fact]
    public async Task Repositories()
    {
        var repoSet = new List<List<string>>()
        {
            new() {"the", "quick", "brown", "fox"},
            new() {"jumps", "over", "the", "lazy"},
            new() {"dog"}
        };
        HttpResponseMessage Func(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Get ||
                req.RequestUri?.AbsolutePath != "/v2/_catalog"
                )
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var q = req.RequestUri.Query;
            try
            {
                var n = int.Parse(NQueryParam().Match(q).Value);
                if (n != 4) throw new Exception();
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var repos = new List<string>();
            var serverUrl = "http://localhost:5000";
            var matched = TestQueryParam().Match(q).Value;
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

            var repositoryList = new Oras.Registry.Remote.Registry.RepositoryList
            {
                Repositories = [.. repos]
            };
            res.Content = new StringContent(JsonSerializer.Serialize(repositoryList));
            return res;

        }

        var registry = new Oras.Registry.Remote.Registry(new RepositoryOptions()
        {
            Reference = new Reference("localhost:5000"),
            PlainHttp = true,
            Client = CustomClient(Func),
            TagListPageSize = 4,
        });
        var cancellationToken = new CancellationToken();

        var wantRepositories = new List<string>();
        foreach (var set in repoSet)
        {
            wantRepositories.AddRange(set);
        }
        var gotRepositories = new List<string>();
        await foreach (var repo in registry.ListRepositoriesAsync().WithCancellation(cancellationToken))
        {
            gotRepositories.Add(repo);
        }
        Assert.Equal(wantRepositories, gotRepositories);
    }

}
