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

using OrasProject.Oras.Interfaces;
using OrasProject.Oras.Oci;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static OrasProject.Oras.Content.Content;

namespace OrasProject.Oras.Memory
{
    internal class MemoryGraph
    {
        private ConcurrentDictionary<BasicDescriptor, ConcurrentDictionary<BasicDescriptor, Descriptor>> _predecessors = new ConcurrentDictionary<BasicDescriptor, ConcurrentDictionary<BasicDescriptor, Descriptor>>();

        internal async Task IndexAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            IList<Descriptor> successors = await SuccessorsAsync(fetcher, node, cancellationToken);
            Index(node, successors, cancellationToken);
        }

        /// <summary>
        /// PredecessorsAsync returns the nodes directly pointing to the current node.
        /// Predecessors returns null without error if the node does not exists in the
        /// store.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<List<Descriptor>> PredecessorsAsync(Descriptor node, CancellationToken cancellationToken)
        {
            var key = node.BasicDescriptor;
            if (!this._predecessors.TryGetValue(key, out ConcurrentDictionary<BasicDescriptor, Descriptor> predecessors))
            {
                return default;
            }
            var res = predecessors.Values.ToList();
            return await Task.FromResult(res);
        }

        /// <summary>
        /// Index indexes predecessors for each direct successor of the given node.
        /// There is no data consistency issue as long as deletion is not implemented
        /// for the underlying storage.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="successors"></param>
        /// <param name="cancellationToken"></param>
        private void Index(Descriptor node, IList<Descriptor> successors, CancellationToken cancellationToken)
        {
            if (successors is null || successors.Count == 0)
            {
                return;
            }

            var predecessorKey = node.BasicDescriptor;
            foreach (var successor in successors)
            {
                var successorKey = successor.BasicDescriptor;
                var predecessors = this._predecessors.GetOrAdd(successorKey, new ConcurrentDictionary<BasicDescriptor, Descriptor>());
                predecessors.TryAdd(predecessorKey, node);
            }

        }
    }
}
