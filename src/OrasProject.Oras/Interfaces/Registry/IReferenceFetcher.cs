using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces.Registry
{
    /// <summary>
    /// IReferenceFetcher provides advanced fetch with the tag service.
    /// </summary>
    public interface IReferenceFetcher
    {
        /// <summary>
        /// FetchReferenceAsync fetches the content identified by the reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<(Descriptor Descriptor, Stream Stream)> FetchReferenceAsync(string reference, CancellationToken cancellationToken = default);
    }
}
