using OrasDotnet.Models;
using System.Threading;
using System.IO;
namespace OrasDotnet.Interfaces
{
    internal interface IReadOnlyStorage
    {
      
        bool Exists(Descriptor target, CancellationToken cancellationToken = default);
      
       Stream Fetch(Descriptor target, CancellationToken cancellationToken = default);
    }
}