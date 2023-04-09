using Oras.Interfaces;
using Oras.Models;
using Oras.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Models
{
    public class MemoryTagResolver : ITagResolver
    {
        
        private ConcurrentDictionary<string, Descriptor> index = new ConcurrentDictionary<string, Descriptor>();
        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {

            var contentExist = index.TryGetValue(reference, out Descriptor content);
            if (!contentExist)
            {
                throw new AlreadyExistsException();
            }
            return Task.FromResult(content);
        }

        public Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {
            index.TryAdd(reference, descriptor);
            return Task.CompletedTask;
        }
    }
}
