using Oras.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Oras.Interfaces
{
    /// <summary>
    /// IResolver resolves reference tags.
    /// </summary>
    public interface IResolver
    {
        /// <summary>
        /// ResolveAsync resolves the tag to a descriptor.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
    }
}
