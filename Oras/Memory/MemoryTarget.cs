using Oras.Exceptions;
using Oras.Interfaces;
using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Memory
{
    public class MemoryTarget : ITarget
    {
        private MemoryStorage _storage  = new MemoryStorage();
        private MemoryTagResolver _tagResolver  = new MemoryTagResolver();
        private MemoryGraph _graph  = new MemoryGraph();
        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.ExistsAsync(target, cancellationToken);
        }

        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.FetchAsync(target, cancellationToken);
        }

        public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            await _storage.PushAsync(expected, contentStream, cancellationToken);
            await _graph.IndexAsync(_storage, expected, cancellationToken);
        }

        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await _tagResolver.ResolveAsync(reference, cancellationToken);
        }


        public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {

            var exists = await _storage.ExistsAsync(descriptor, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"{descriptor.Digest} : {descriptor.MediaType}");
            }
            await _tagResolver.TagAsync(descriptor, reference, cancellationToken);

        }
    }
}
