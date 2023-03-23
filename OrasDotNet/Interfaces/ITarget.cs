using OrasDotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    internal interface ITarget : IStorage
    {
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
        Task TagAsync(Descriptor descriptor, CancellationToken cancellationToken = default);
        
    }
}