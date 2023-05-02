﻿using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// Repository is an ORAS target and an union of the blob and the manifest CASs.
    /// As specified by https://docs.docker.com/registry/spec/api/, it is natural to
    /// assume that content.Resolver interface only works for manifests. Tagging a
    /// blob may be resulted in an `ErrUnsupported` error. However, this interface
    /// does not restrict tagging blobs.
    /// Since a repository is an union of the blob and the manifest CASs, all
    /// operations defined in the `BlobStore` are executed depending on the media
    /// type of the given descriptor accordingly.
    /// Furthermore, this interface also provides the ability to enforce the
    /// separation of the blob and the manifests CASs.
    /// </summary>
    internal interface IRepository : ITarget, ITagResolver, IReferenceFetcher, IReferencePusher, IReferrerLister, IDeleter
    {
        /// <summary>
        /// BlobsAsync provides access to the blob CAS only, which contains config blobs,layers, and other generic blobs.
        /// </summary>
        /// <returns></returns>
        Task<IBlobTarget> BlobsAsync();
        /// <summary>
        /// ManifestsAsync provides access to the manifest CAS only.
        /// </summary>
        /// <returns></returns>
        Task<IManifestTarget> ManifestsAsync();
    }
}
