using OrasDotnet.Interfaces;
using OrasDotNet.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotNet.Models
{
    internal class MemoryTagResolver : ITagResolver
    {

        public ConcurrentDictionary<string, Descriptor> index = new ConcurrentDictionary<string, Descriptor>();
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
