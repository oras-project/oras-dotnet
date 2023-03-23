using OrasDotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace OrasDotNet.Interfaces
{
    internal interface ITagResolver
    {
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
        Task TagAsync(Descriptor descriptor, CancellationToken cancellationToken = default);
    }
}
