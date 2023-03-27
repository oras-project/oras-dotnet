using OrasDotnet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace OrasDotnet.Interfaces
{
    public interface IResolver
    {
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
    }
}
