using OrasDotNet.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    internal interface IReadOnlyStorage
    {
        Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default);
        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);
    }

}