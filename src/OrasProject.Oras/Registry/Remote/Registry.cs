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

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OrasProject.Oras.Remote;

public class Registry : IRegistry
{
    public RepositoryOptions RepositoryOptions => _opts;

    internal RepositoryOptions _opts;

    public Registry(string registry) : this(registry, new HttpClient().AddUserAgent()) { }

    public Registry(string registry, HttpClient httpClient)
    {
        _opts = new()
        {
            Reference = new Reference(registry),
            HttpClient = httpClient,
        };
    }

    public Registry(RepositoryOptions options) => _opts = options;

    /// <summary>
    /// PingAsync checks whether or not the registry implement Docker Registry API V2 or
    ///  OCI Distribution Specification.
    ///  Ping can be used to check authentication when an auth client is configured.
    ///  References:
    ///   - https://docs.docker.com/registry/spec/api/#base
    ///   - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#api
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PingAsync(CancellationToken cancellationToken)
    {
        var url = URLUtiliity.BuildRegistryBaseURL(_opts.PlainHttp, _opts.Reference);
        using var resp = await _opts.HttpClient.GetAsync(url, cancellationToken);
        switch (resp.StatusCode)
        {
            case HttpStatusCode.OK:
                return;
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"Repository {_opts.Reference} not found");
            default:
                throw await ErrorUtility.ParseErrorResponse(resp);
        }
    }

    /// <summary>
    /// Repository returns a repository object for the given repository name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IRepository> GetRepository(string name, CancellationToken cancellationToken)
    {
        var reference = new Reference(_opts.Reference.Registry, name);
        var options = _opts; // shallow copy
        options.Reference = reference;
        return Task.FromResult<IRepository>(new Repository(options));
    }


    /// <summary>
    /// Repositories returns a list of repositories from the remote registry.
    /// </summary>
    /// <param name="last"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> ListRepositoriesAsync(string? last = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = URLUtiliity.BuildRegistryCatalogURL(_opts.PlainHttp, _opts.Reference);
        var done = false;
        while (!done)
        {
            IEnumerable<string> repositories = Array.Empty<string>();
            try
            {
                url = await RepositoryPageAsync(last, values => repositories = values, url, cancellationToken);
                last = "";
            }
            catch (LinkUtility.NoLinkHeaderException)
            {
                done = true;
            }
            foreach (var repository in repositories)
            {
                yield return repository;
            }
        }
    }

    /// <summary>
    /// RepositoryPageAsync returns a returns a single page of repositories list with the next link
    /// </summary>
    /// <param name="last"></param>
    /// <param name="fn"></param>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<string> RepositoryPageAsync(string? last, Action<string[]> fn, string url, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        if (_opts.TagListPageSize > 0 || !string.IsNullOrEmpty(last))
        {
            if (_opts.TagListPageSize > 0)
            {
                query["n"] = _opts.TagListPageSize.ToString();


            }
            if (!string.IsNullOrEmpty(last))
            {
                query["last"] = last;
            }
        }

        uriBuilder.Query = query.ToString();
        using var response = await _opts.HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await ErrorUtility.ParseErrorResponse(response);

        }
        var data = await response.Content.ReadAsStringAsync();
        var repositories = JsonSerializer.Deserialize<ResponseTypes.RepositoryList>(data);
        fn(repositories.Repositories);
        return LinkUtility.ParseLink(response);
    }
}
