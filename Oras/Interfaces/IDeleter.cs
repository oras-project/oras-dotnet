using Oras.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IDeleter removes content.
    /// </summary>
    public interface IDeleter
    {
        /// <summary>
        /// This deletes content Identified by the descriptor
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
