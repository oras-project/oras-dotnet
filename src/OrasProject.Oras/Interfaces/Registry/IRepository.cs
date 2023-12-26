namespace OrasProject.Oras.Interfaces.Registry
{
    /// <summary>
    /// Repository is an ORAS target and an union of the blob and the manifest CASs.
    /// As specified by https://docs.docker.com/registry/spec/api/, it is natural to
    /// assume that IResolver interface only works for manifests. Tagging a
    /// blob may be resulted in an `UnsupportedException` error. However, this interface
    /// does not restrict tagging blobs.
    /// Since a repository is an union of the blob and the manifest CASs, all
    /// operations defined in the `IBlobStore` are executed depending on the media
    /// type of the given descriptor accordingly.
    /// Furthermore, this interface also provides the ability to enforce the
    /// separation of the blob and the manifests CASs.
    /// </summary>
    public interface IRepository : ITarget, IReferenceFetcher, IReferencePusher, IDeleter, ITagLister
    {
        /// <summary>
        /// Blobs provides access to the blob CAS only, which contains config blobs,layers, and other generic blobs.
        /// </summary>
        /// <returns></returns>
        IBlobStore Blobs();
        /// <summary>
        /// Manifests provides access to the manifest CAS only.
        /// </summary>
        /// <returns></returns>
        IManifestStore Manifests();
    }
}
