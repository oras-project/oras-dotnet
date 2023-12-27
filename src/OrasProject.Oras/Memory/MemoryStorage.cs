// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Interfaces;
using OrasProject.Oras.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static OrasProject.Oras.Content.Content;

namespace OrasProject.Oras.Memory
{
    internal class MemoryStorage : IStorage
    {
        private ConcurrentDictionary<MinimumDescriptor, byte[]> _content = new ConcurrentDictionary<MinimumDescriptor, byte[]>();

        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken)
        {
            var contentExist = _content.ContainsKey(target.GetMinimumDescriptor());
            return Task.FromResult(contentExist);
        }



        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = this._content.TryGetValue(target.GetMinimumDescriptor(), out byte[] content);
            if (!contentExist)
            {
                throw new NotFoundException($"{target.Digest} : {target.MediaType}");
            }
            return Task.FromResult<Stream>(new MemoryStream(content));
        }


        public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            var key = expected.GetMinimumDescriptor();
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
