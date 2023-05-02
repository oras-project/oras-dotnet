using System.Threading.Tasks;

namespace Oras.Interfaces
{
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
