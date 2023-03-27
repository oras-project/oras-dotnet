using OrasDotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using OrasDotnet.Models;

namespace OrasDotnet.Interfaces
{
<<<<<<< HEAD
    internal interface ITagResolver
    {
        Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);
        Task TagAsync(string reference, Descriptor descriptor, CancellationToken cancellationToken = default);
=======
    public interface ITagResolver : IResolver
    {
        Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default);
>>>>>>> interface
    }
}
