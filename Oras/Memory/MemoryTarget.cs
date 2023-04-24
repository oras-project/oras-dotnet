﻿using Oras.Interfaces;
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
        private MemoryStorage _storage { get; set; } = new MemoryStorage();
        private MemoryTagResolver _tagResolver { get; set; } = new MemoryTagResolver();
        private MemoryGraph _graph { get; set; } = new MemoryGraph();
        async public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.ExistsAsync(target, cancellationToken);
        }

        async public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.FetchAsync(target, cancellationToken);
        }

        async public Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            await _storage.PushAsync(expected, contentStream, cancellationToken);
            await _graph.IndexAsync(_storage, expected, cancellationToken);
        }

        async public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await ResolveAsync(reference, cancellationToken);
        }


        async public Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _storage.ExistsAsync(descriptor, cancellationToken);
            }
            catch (Exception)
            {
                throw new NotFoundException($"{descriptor.Digest} : {descriptor.MediaType}");
            }
            await _tagResolver.TagAsync(descriptor, reference, cancellationToken);
        }
    }
}
