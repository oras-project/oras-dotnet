using OrasDotnet.Models;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    public interface IReadOnlyStorage
    {

        Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default);

        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
