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
        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);

    }
}
