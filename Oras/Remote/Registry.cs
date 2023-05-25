using Oras.Exceptions;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Remote
{
    public class Registry
    {

        public HttpClient HttpClient { get; set; }
        public RemoteReference RemoteReference { get; set; }
        public bool PlainHTTP { get; set; }
        public string[] ManifestMediaTypes { get; set; }
        public int TagListPageSize { get; set; }
        public long MaxMetadataBytes { get; set; }

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
                    throw await ErrorUtil.ParseErrorResponse(resp);
            }
        }
    }
}
