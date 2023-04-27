using Oras.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IResolver resolves reference tags.
    /// </summary>
    interface IResolver
    {
        /// <summary>
        /// ResolveAsync resolves the reference to a descriptor.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
    }
}
