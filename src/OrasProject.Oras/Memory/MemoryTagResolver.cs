using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Interfaces;
using OrasProject.Oras.Models;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Memory
{
    internal class MemoryTagResolver : ITagResolver
    {

        private ConcurrentDictionary<string, Descriptor> _index = new ConcurrentDictionary<string, Descriptor>();
        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {

            var contentExist = _index.TryGetValue(reference, out Descriptor content);
            if (!contentExist)
            {
                throw new NotFoundException();
            }
            return Task.FromResult(content);
        }

        public Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {
            _index.AddOrUpdate(reference, descriptor, (key, oldValue) => descriptor);
            return Task.CompletedTask;
        }
    }
}
