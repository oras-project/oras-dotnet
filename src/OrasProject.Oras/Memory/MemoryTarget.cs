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
using OrasProject.Oras.Oci;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Memory
{
    public class MemoryTarget : ITarget
    {
        private MemoryStorage _storage = new MemoryStorage();
        private MemoryTagResolver _tagResolver = new MemoryTagResolver();
        private MemoryGraph _graph = new MemoryGraph();

        /// <summary>
        /// ExistsAsync returns checks if the described content exists.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.ExistsAsync(target, cancellationToken);
        }

        /// <summary>
        /// FetchAsync fetches the content identified by the descriptor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            return await _storage.FetchAsync(target, cancellationToken);
        }

        /// <summary>
        /// PushAsync pushes the content, matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="contentStream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
            await _storage.PushAsync(expected, contentStream, cancellationToken);
            await _graph.IndexAsync(_storage, expected, cancellationToken);
        }

        /// <summary>
        /// ResolveAsync resolves a reference to a descriptor.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            return await _tagResolver.ResolveAsync(reference, cancellationToken);
        }

        /// <summary>
        /// TagAsync tags a descriptor with a reference string.
        /// It throws NotFoundException if the tagged content does not exist.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
        {

            var exists = await _storage.ExistsAsync(descriptor, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"{descriptor.Digest} : {descriptor.MediaType}");
            }
            await _tagResolver.TagAsync(descriptor, reference, cancellationToken);

        }

        /// <summary>
        /// PredecessorsAsync returns the nodes directly pointing to the current node.
        /// Predecessors returns null without error if the node does not exists in the
        /// store.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<Descriptor>> PredecessorsAsync(Descriptor node, CancellationToken cancellationToken = default)
        {
            return await _graph.PredecessorsAsync(node, cancellationToken);
        }
    }
}
