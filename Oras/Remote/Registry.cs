using Oras.Exceptions;
using Oras.Interfaces.Registry;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Web.HttpUtility;

namespace Oras.Remote
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
            HttpClient.DefaultRequestHeaders.Add("User-Agent", new string[] { "oras-dotnet" });
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
        public Task<IRepository> Repository(string name, CancellationToken cancellationToken)
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
        /// <param name="fn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task Repositories(string last, Action<string[]> fn, CancellationToken cancellationToken)
        {
            try
            {
                var url = URLUtiliity.BuildRegistryCatalogURL(PlainHTTP, RemoteReference);
                while (true)
                {
                    url = await RepositoryPageAsync(last, fn, url, cancellationToken);
                    last = "";
                }
            }
            catch (LinkUtility.NoLinkHeaderException)
            {
                return;
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
        private async Task<string> RepositoryPageAsync(string last, Action<string[]> fn, string url, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(url);
            var query = ParseQueryString(uriBuilder.Query);
            if (TagListPageSize > 0 || last != "")
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
