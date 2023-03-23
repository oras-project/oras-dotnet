using OrasDotNet.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    internal interface IStorage : IReadOnlyStorage
    {
        Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default);
    }

}