using Oras.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Oras.Interfaces
{
    public interface IFetcher
    {
        /// <summary>
        /// FetchAsync fetches the _content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);

    }
}
