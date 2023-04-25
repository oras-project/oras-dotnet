using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
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
