using Oras.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// ITagResolver provides reference tag indexing services.
    /// </summary>
    public interface ITagResolver : IResolver
    {
        /// <summary>
        /// TagAsync tags the descriptor with the reference.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default);
    }
}
