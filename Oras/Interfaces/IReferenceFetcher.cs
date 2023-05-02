using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IReferenceFetcher provides advanced fetch with the tag service.
    /// </summary>
    internal interface IReferenceFetcher
    {
        /// <summary>
        /// FetchReferenceAsync fetches the content identified by the reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        (Task<Descriptor>, Task<Stream>) FetchReferenceAsync(string reference, CancellationToken cancellationToken = default);
    }
}
