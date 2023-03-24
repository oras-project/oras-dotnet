using OrasDotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using OrasDotnet.Models;

namespace OrasDotnet.Interfaces
{
    public interface ITagResolver : IResolver
    {
        Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default);
    }
}
