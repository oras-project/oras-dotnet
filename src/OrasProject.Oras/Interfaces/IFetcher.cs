using OrasProject.Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Interfaces
{
    /// <summary>
    /// IFetcher fetches content.
    /// </summary>
    public interface IFetcher
    {
        /// <summary>
        /// FetchAsync fetches the content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
