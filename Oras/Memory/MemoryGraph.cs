﻿using Oras.Content;
using Oras.Interfaces;
using Oras.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Memory
{
    public class MemoryGraph
    {
        private ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>> _predecessors = new ConcurrentDictionary<MinimumDescriptor, ConcurrentDictionary<MinimumDescriptor, Descriptor>>();
        private ConcurrentDictionary<MinimumDescriptor, object> _indexed = new ConcurrentDictionary<MinimumDescriptor, object>();

        async public Task IndexAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            IList<Descriptor> successors = await StorageUtility.SuccessorsAsync(fetcher, node, cancellationToken);
            Index(node, successors, cancellationToken);
        }


        /// <summary>
        /// Index indexes predecessors for each direct successor of the given node.
        /// There is no data consistency issue as long as deletion is not implemented
        /// for the underlying _storage.
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