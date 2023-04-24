using Oras.Interfaces;
using Oras.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Memory
{
    public class MemoryGraph
    {
        private ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>> predecessors = new ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>>();
        private ConcurrentDictionary<MinimumDescriptor, object> indexed = new ConcurrentDictionary<MinimumDescriptor, object>();

        async public Task IndexAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            IList<Descriptor> successors = await node.SuccessorsAsync(fetcher, node, cancellationToken);
            Index(node, successors, cancellationToken);
        }

        // index indexes predecessors for each direct successor of the given node.
        // There is no data consistency issue as long as deletion is not implemented
        // for the underlying storage.
        private void Index(Descriptor node, IList<Descriptor> successors, CancellationToken cancellationToken)
        {
            if (successors.Count == 0)
            {
                {
                    return;
                }
            }
            var predecessorKey = Descriptor.FromOCI(node);
            foreach (var successor in successors)
            {
                var successorKey = Descriptor.FromOCI(successor);
                var predecessors = this.predecessors.GetOrAdd(successorKey, new ConcurrentDictionary<MinimumDescriptor, Descriptor>());
                predecessors.TryAdd(predecessorKey, node);
            }

        }
    }
}