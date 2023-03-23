using OrasDotnet.Interfaces;
using OrasDotnet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotNet.Models
{
    internal class MemoryTarget : ITarget
    {
        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task TagAsync(Descriptor descriptor, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
