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

using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Memory
{
    internal class MemoryTagResolver : ITagStore
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
