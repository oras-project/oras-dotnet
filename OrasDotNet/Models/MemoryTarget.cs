using OrasDotnet.Interfaces;
using OrasDotnet.Models;
using OrasDotNet.Models.Errors;
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
        public MemoryStorage Storage { get; set; } = new MemoryStorage();
        public MemoryTagResolver TagResolver { get; set; } = new MemoryTagResolver();
        public MemoryGraph Graph { get; set; } = new MemoryGraph();
        async public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await Storage.ExistsAsync(target, cancellationToken);
        }

        async public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await Storage.FetchAsync(target, cancellationToken);
        }

        async public Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            await Storage.PushAsync(expected, contentStream, cancellationToken);
            // index predecessors
        }

        async public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await ResolveAsync(reference, cancellationToken);
        }

        async public Task TagAsync(string reference, Descriptor descriptor, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await Storage.ExistsAsync(descriptor, cancellationToken);
            }
            catch (Exception)
            {
                throw new Exception($"{descriptor.Digest} : {descriptor.MediaType} : {new NotFoundException().Message}");
            }
            await TagResolver.TagAsync(reference, descriptor, cancellationToken);
        }
    }
}
