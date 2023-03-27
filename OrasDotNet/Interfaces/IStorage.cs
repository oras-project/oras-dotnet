using OrasDotnet.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
<<<<<<< HEAD
    internal interface IStorage : IReadOnlyStorage
=======
    public interface IStorage : IReadOnlyStorage
>>>>>>> interface
    {
        Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default);
    }

}
