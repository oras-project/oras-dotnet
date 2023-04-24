using Oras.Interfaces;
using Oras.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oras.Models;

namespace Oras.Memory
{
    public class MemoryTarget : ITarget
    {
        private MemoryStorage storage { get; set; } = new MemoryStorage();
        private MemoryTagResolver tagResolver { get; set; } = new MemoryTagResolver();
        private MemoryGraph graph { get; set; } = new MemoryGraph();
        async public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await storage.ExistsAsync(target, cancellationToken);
        }

        async public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await storage.FetchAsync(target, cancellationToken);
        }

        async public Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            await storage.PushAsync(expected, contentStream, cancellationToken);
            await graph.IndexAsync(storage, expected, cancellationToken);
        }

        async public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await ResolveAsync(reference, cancellationToken);
        }


        async public Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await storage.ExistsAsync(descriptor, cancellationToken);
            }
            catch (Exception)
            {
                throw new NotFoundException($"{descriptor.Digest} : {descriptor.MediaType}");
            }
            await tagResolver.TagAsync(descriptor, reference, cancellationToken);
        }
    }
}
