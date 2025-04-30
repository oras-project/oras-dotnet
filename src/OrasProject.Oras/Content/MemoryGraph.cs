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

using OrasProject.Oras.Oci;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

internal class MemoryGraph : IPredecessorFindable
{
    private readonly ConcurrentDictionary<BasicDescriptor, ConcurrentDictionary<BasicDescriptor, Descriptor>> _predecessors = new();

    /// <summary>
    /// Returns the nodes directly pointing to the current node.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public Task<IEnumerable<Descriptor>> GetPredecessorsAsync(Descriptor node, CancellationToken cancellationToken = default)
    {
        var key = node.BasicDescriptor;
        if (_predecessors.TryGetValue(key, out var predecessors))
        {
            return Task.FromResult<IEnumerable<Descriptor>>(predecessors.Values);
        }
        return Task.FromResult<IEnumerable<Descriptor>>([]);
    }

    internal async Task IndexAsync(IFetchable fetcher, Descriptor node, CancellationToken cancellationToken)
    {
        var successors = await fetcher.GetSuccessorsAsync(node, cancellationToken).ConfigureAwait(false);
        Index(node, successors);
    }

    /// <summary>
    /// Index indexes predecessors for each direct successor of the given node.
    /// There is no data consistency issue as long as deletion is not implemented
    /// for the underlying storage.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="successors"></param>
    private void Index(Descriptor node, IEnumerable<Descriptor> successors)
    {
        var predecessorKey = node.BasicDescriptor;
        foreach (var successor in successors)
        {
            var successorKey = successor.BasicDescriptor;
            var predecessors = _predecessors.GetOrAdd(successorKey, _ => new ConcurrentDictionary<BasicDescriptor, Descriptor>());
            predecessors.TryAdd(predecessorKey, node);
        }
    }
}
