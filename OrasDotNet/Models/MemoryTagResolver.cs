using OrasDotnet.Interfaces;
using OrasDotnet.Models;
using OrasDotNet.Models.Errors;
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

        public ConcurrentDictionary<string, Descriptor> Index = new ConcurrentDictionary<string, Descriptor>();
        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {

            var contentExist = Index.TryGetValue(reference, out Descriptor content);
            if (!contentExist)
            {
                throw new Exception($"{new AlreadyExistsException().Message}");
            }
            return Task.FromResult(content);
        }

        public Task TagAsync(string reference, Descriptor descriptor, CancellationToken cancellationToken = default)
        {
            Index.TryAdd(reference, descriptor);
            return Task.CompletedTask;
        }
    }
}
