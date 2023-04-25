using Oras.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IReadOnlyStorage represents a read-only Storage.
    /// </summary>
    public interface IReadOnlyStorage : IFetcher
    {
        /// <summary>
        /// ExistsAsync returns true if the described _content exists.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
