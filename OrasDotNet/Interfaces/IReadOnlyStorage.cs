using OrasDotnet.Models;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    public interface IReadOnlyStorage : IFetcher
    {
        Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
