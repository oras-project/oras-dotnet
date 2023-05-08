namespace Oras.Remote
{
    /// <summary>
    /// Repository is an HTTP client to a remote repository
    /// </summary>
    public class Repository
    {

        // Client is the underlying HTTP client used to access the remote registry.
        // If nil, auth.DefaultClient is used.
        public
        Client Client

    // Reference references the remote repository.
    Reference registry.Reference

    // PlainHTTP signals the transport to access the remote repository via HTTP
    // instead of HTTPS.
    PlainHTTP bool

    // ManifestMediaTypes is used in `Accept` header for resolving manifests
    // from references. It is also used in identifying manifests and blobs from
    // descriptors. If an empty list is present, default manifest media types
    // are used.
    ManifestMediaTypes []string

    // TagListPageSize specifies the page size when invoking the tag list API.
    // If zero, the page size is determined by the remote registry.
    // Reference: https://docs.docker.com/registry/spec/api/#tags
    TagListPageSize int

    // ReferrerListPageSize specifies the page size when invoking the Referrers
    // API.
    // If zero, the page size is determined by the remote registry.
    // Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0-rc1/spec.md#listing-referrers
    ReferrerListPageSize int

    // MaxMetadataBytes specifies a limit on how many response bytes are allowed
    // in the server's response to the metadata APIs, such as catalog list, tag
    // list, and referrers list.
    // If less than or equal to zero, a default (currently 4MiB) is used.
    MaxMetadataBytes int64

    // NOTE: Must keep fields in sync with newRepositoryWithOptions function.

    // referrersState represents that if the repository supports Referrers API.
    // default: referrersStateUnknown
    referrersState referrersState

    // referrersPingLock locks the pingReferrers() method and allows only
    // one go-routine to send the request.
    referrersPingLock sync.Mutex

    // referrersMergePool provides a way to manage concurrent updates to a
    // referrers index tagged by referrers tag schema.
    referrersMergePool syncutil.Pool[syncutil.Merge[referrerChange]]
}
}
}
