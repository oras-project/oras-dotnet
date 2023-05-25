using Oras.Exceptions;
using Oras.Interfaces;
using Oras.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Oras.Content.Content;

namespace Oras.Memory
{
    internal class MemoryStorage : IStorage
    {
        private ConcurrentDictionary<MinimumDescriptor, byte[]> _content = new ConcurrentDictionary<MinimumDescriptor, byte[]>();

        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken)
        {
            var contentExist = _content.ContainsKey(target.GetMinimum());
            return Task.FromResult(contentExist);
        }



        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = this._content.TryGetValue(target.GetMinimum(), out byte[] content);
            if (!contentExist)
            {
                throw new NotFoundException($"{target.Digest} : {target.MediaType}");
            }
            return Task.FromResult<Stream>(new MemoryStream(content));
        }


        public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            var key = expected.GetMinimum();
            var contentExist = _content.TryGetValue(key, out byte[] _);
            if (contentExist)
            {
                throw new AlreadyExistsException($"{expected.Digest} : {expected.MediaType}");
            }
            var readBytes = await ReadAllAsync(contentStream, expected);

            var added = _content.TryAdd(key, readBytes);
            if (!added) throw new AlreadyExistsException($"{key.Digest} : {key.MediaType}");
            return;
        }
    }
}
