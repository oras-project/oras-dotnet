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
    public class MemoryStorage : IStorage
    {
        private ConcurrentDictionary<MinimumDescriptor, byte[]> content { get; set; } = new ConcurrentDictionary<MinimumDescriptor, byte[]>();

        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = content.ContainsKey(Descriptor.FromOCI(target));
            return Task.FromResult(contentExist);
        }



        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = this.content.TryGetValue(Descriptor.FromOCI(target), out byte[] content);
            if (!contentExist)
            {
                throw new NotFoundException($"{target.Digest} : {target.MediaType}");
            }
            return Task.FromResult<Stream>(new MemoryStream(content));
        }


        public Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            var key = Descriptor.FromOCI(expected);
            var contentExist = this.content.TryGetValue(key, out byte[] _);
            if (!contentExist)
            {
                throw new Exception($"{expected.Digest} : {expected.MediaType} : {new AlreadyExistsException().Message}");
            }

            using (var memoryStream = new MemoryStream())
            {
                contentStream.CopyTo(memoryStream);
              var exists = this.content.TryAdd(key, memoryStream.ToArray());
                if (!exists) throw new AlreadyExistsException($"{key.Digest} : {key.MediaType}");
            }
            return Task.CompletedTask;
        }
    }
}
