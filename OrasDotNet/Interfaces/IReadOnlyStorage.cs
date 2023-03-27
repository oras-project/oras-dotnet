using OrasDotnet.Models;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
<<<<<<< HEAD
    internal interface IReadOnlyStorage
=======
    public interface IReadOnlyStorage
>>>>>>> interface
    {

        Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default);

        Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
