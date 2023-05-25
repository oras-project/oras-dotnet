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
    public class Registry : IRepositoryOption
    {

        public HttpClient HttpClient { get; set; }
        public RemoteReference RemoteReference { get; set; }
        public bool PlainHTTP { get; set; }
        public string[] ManifestMediaTypes { get; set; }
        public int TagListPageSize { get; set; }

        /// <summary>
        /// Client returns an HTTP client used to access the remote repository.
        /// A default HTTP client is return if the client is not configured.
        /// </summary>
        /// <returns></returns>
        private HttpClient Client()
        {
            if (HttpClient is null)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", new string[] { "oras-dotnet" });
                return client;
            }

            return HttpClient;
        }

        public Registry(string name)
        {
            var reference = new RemoteReference
            {
                Registry = name,
            };
            reference.ValidateRegistry();
            RemoteReference = reference;
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
            var resp = await Client().GetAsync(url, cancellationToken);
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
        /// ListRepositoriesAsync returns a list of repositories from the remote registry.
        /// </summary>
        /// <param name="last"></param>
        /// <param name="fn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ListRepositoriesAsync(string last, Action<string[]> fn, CancellationToken cancellationToken)
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
            catch (Utils.NoLinkHeaderException)
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
            if (PlainHTTP)
            {
                if (!url.Contains("http"))
                {
                    url = "http://" + url;
                }
            }
            else
            {
                if (!url.Contains("https"))
                {
                    url = "https://" + url;
                }
            }
            var uriBuilder = new UriBuilder(url);
            var query = ParseQueryString(uriBuilder.Query);
            if (TagListPageSize > 0 || last != "")
            {
                if (TagListPageSize > 0)
                {
                    query["n"] = TagListPageSize.ToString();


                }
                if (last != "")
                {
                    query["last"] = last;
                }
            }

            uriBuilder.Query = query.ToString();
            var response = await HttpClient.GetAsync(uriBuilder.ToString(), cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw await ErrorUtility.ParseErrorResponse(response);

            }
            var data = await response.Content.ReadAsStringAsync();
            var repositories = JsonSerializer.Deserialize<string[]>(data);
            fn(repositories);
            return Utils.ParseLink(response);
        }
    }
}
