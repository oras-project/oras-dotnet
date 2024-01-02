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
using static System.Web.HttpUtility;

namespace OrasProject.Oras.Remote
{
    public class Registry : IRegistry, IRepositoryOption
    {

        public HttpClient HttpClient { get; set; }
        public RemoteReference RemoteReference { get; set; }
        public bool PlainHTTP { get; set; }
        public string[] ManifestMediaTypes { get; set; }
        public int TagListPageSize { get; set; }

        public Registry(string name)
        {
            var reference = new RemoteReference
            {
                Registry = name,
            };
            reference.ValidateRegistry();
            RemoteReference = reference;
            HttpClient = new HttpClient();
            HttpClient.AddUserAgent();
        }

        public Registry(string name, HttpClient httpClient)
        {
            var reference = new RemoteReference
            {
                Registry = name,
            };
            reference.ValidateRegistry();
            RemoteReference = reference;
            HttpClient = httpClient;
        }

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
            var url = URLUtiliity.BuildRegistryBaseURL(PlainHTTP, RemoteReference);
            using var resp = await HttpClient.GetAsync(url, cancellationToken);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                    return;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"Repository {RemoteReference} not found");
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
            var reference = new RemoteReference
            {
                Registry = RemoteReference.Registry,
                Repository = name,
            };

            return Task.FromResult<IRepository>(new Repository(reference, this));
        }


        /// <summary>
        /// Repositories returns a list of repositories from the remote registry.
        /// </summary>
        /// <param name="last"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<string> ListRepositoriesAsync(string? last = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var url = URLUtiliity.BuildRegistryCatalogURL(PlainHTTP, RemoteReference);
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
            var query = ParseQueryString(uriBuilder.Query);
            if (TagListPageSize > 0 || !string.IsNullOrEmpty(last))
            {
                if (TagListPageSize > 0)
                {
                    query["n"] = TagListPageSize.ToString();


                }
                if (!string.IsNullOrEmpty(last))
                {
                    query["last"] = last;
                }
            }

            uriBuilder.Query = query.ToString();
            using var response = await HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken);
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
}
