using Oras.Content;
using Oras.Interfaces;
using Oras.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Memory
{
    internal class MemoryGraph
    {
        private ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>> _predecessors = new ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>>();

        internal async Task IndexAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            IList<Descriptor> successors = await GraphUtility.SuccessorsAsync(fetcher, node, cancellationToken);
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
            var minimumDescriptor = Descriptor.FromOCI(node);
            if (!this._predecessors.TryGetValue(minimumDescriptor, out ConcurrentDictionary<MinimumDescriptor, Descriptor> predecessors))
            {
                return null;
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
            if (successors is null)
            {
                return;
            }
            var predecessorKey = Descriptor.FromOCI(node);
            foreach (var successor in successors)
            {
                var successorKey = Descriptor.FromOCI(successor);
                var predecessors = this._predecessors.GetOrAdd(successorKey, new ConcurrentDictionary<MinimumDescriptor, Descriptor>());
                predecessors.TryAdd(predecessorKey, node);
            }

        }
    }
}
