using Oras.Remote;
using System.Net.Http;

namespace Oras.Interfaces.Registry
{
    /// <summary>
    /// IRepositoryOption is used to configure a remote repository.
    /// </summary>
    public interface IRepositoryOption
    {
        /// <summary>
        /// Client is the underlying HTTP client used to access the remote registry.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// Reference references the remote repository.
        /// </summary>
        public RemoteReference RemoteReference { get; set; }

        /// <summary>
        /// PlainHTTP signals the transport to access the remote repository via HTTP
        /// instead of HTTPS.
        /// </summary>
        public bool PlainHTTP { get; set; }


        /// <summary>
        /// ManifestMediaTypes is used in `Accept` header for resolving manifests
        /// from references. It is also used in identifying manifests and blobs from
        /// descriptors. If an empty list is present, default manifest media types
        /// are used.
        /// </summary>
        public string[] ManifestMediaTypes { get; set; }

        /// <summary>
        /// TagListPageSize specifies the page size when invoking the tag list API.
        /// If zero, the page size is determined by the remote registry.
        /// Reference: https://docs.docker.com/registry/spec/api/#tags
        /// </summary>
        public int TagListPageSize { get; set; }

        /// <summary>
        /// MaxMetadataBytes specifies a limit on how many response bytes are allowed
        /// in the server's response to the metadata APIs, such as catalog list, tag
        /// list, and referrers list.
        /// If less than or equal to zero, a default (currently 4MiB) is used.
        /// </summary>
        public long MaxMetadataBytes { get; set; }

    }
}
