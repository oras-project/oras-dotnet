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
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;

namespace OrasProject.Oras;

public static class ReadOnlyGraphTargetExtensions
{
    /// <summary>
    /// Represents a node in the copy operation with depth information.
    /// </summary>
    internal class NodeInfo
    {
        public Descriptor Node { get; set; } = default!;
        public int Depth { get; set; }
    }

    /// <summary>
    /// FindRoots finds the root nodes reachable from the given node through a
    /// depth-first search.
    /// </summary>
    /// <param name="storage">The storage containing the graph</param>
    /// <param name="node">The starting node for the search</param>
    /// <param name="opts">Extended copy graph options containing search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of root descriptors found</returns>
    /// <exception cref="InvalidOperationException">Thrown when FindPredecessors operation fails</exception>
    public static async Task<List<Descriptor>> FindRootsAsync(
        this IReadOnlyGraphTarget storage,
        Descriptor node,
        ExtendedCopyGraphOptions opts,
        CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<BasicDescriptor>();
        var rootMap = new Dictionary<BasicDescriptor, Descriptor>();

        void AddRoot(BasicDescriptor key, Descriptor val)
        {
            if (!rootMap.ContainsKey(key))
            {
                rootMap[key] = val;
            }
        }

        // If FindPredecessors is not provided, use the default one
        Func<IPredecessorFindable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>> findPredecessors =
            opts.FindPredecessors ?? DefaultFindPredecessors;

        var stack = new Stack<NodeInfo>();
        // Push the initial node to the stack, set the depth to 0
        stack.Push(new NodeInfo { Node = node, Depth = 0 });

        while (stack.TryPop(out var current))
        {
            var currentNode = current.Node;
            var currentKey = currentNode.BasicDescriptor;

            if (visited.Contains(currentKey))
            {
                // Skip the current node if it has been visited
                continue;
            }
            visited.Add(currentKey);

            // Stop finding predecessors if the target depth is reached
            if (opts.Depth > 0 && current.Depth == opts.Depth)
            {
                AddRoot(currentKey, currentNode);
                continue;
            }

            IEnumerable<Descriptor> predecessors;
            try
            {
                predecessors = await findPredecessors(storage, currentNode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("FindPredecessors operation failed", ex);
            }

            var predecessorList = predecessors.ToList();

            // The current node has no predecessor node,
            // which means it is a root node of a sub-DAG.
            if (predecessorList.Count == 0)
            {
                AddRoot(currentKey, currentNode);
                continue;
            }

            // The current node has predecessor nodes, which means it is NOT a root node.
            // Push the predecessor nodes to the stack and keep finding from there.
            foreach (var predecessor in predecessorList)
            {
                var predecessorKey = predecessor.BasicDescriptor;
                if (!visited.Contains(predecessorKey))
                {
                    // Push the predecessor node with increased depth
                    stack.Push(new NodeInfo { Node = predecessor, Depth = current.Depth + 1 });
                }
            }
        }

        var roots = new List<Descriptor>(rootMap.Values);
        return roots;
    }

    /// <summary>
    /// Default implementation for finding predecessors.
    /// </summary>
    /// <param name="storage">The storage to search in</param>
    /// <param name="descriptor">The descriptor to find predecessors for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enumerable of predecessor descriptors</returns>
    private static async Task<IEnumerable<Descriptor>> DefaultFindPredecessors(
        IPredecessorFindable storage,
        Descriptor descriptor,
        CancellationToken cancellationToken)
    {
        return await storage.GetPredecessorsAsync(descriptor, cancellationToken).ConfigureAwait(false);
    }
    
    internal static async Task ExtendedCopyGraphAsync(this IReadOnlyGraphTarget src, ITarget dst, Descriptor node, Proxy proxy, ExtendedCopyGraphOptions extendedCopyGraphOptions, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        var roots = await FindRootsAsync(src, node, extendedCopyGraphOptions, cancellationToken).ConfigureAwait(false);

        var childNodesCopies = new List<Task>();
        foreach (var childNode in roots)
        {
            childNodesCopies.Add(Task.Run(async () =>
                    await src.CopyGraphAsync(dst, childNode, proxy, extendedCopyGraphOptions, limiter, cancellationToken).ConfigureAwait(false), cancellationToken));
        }
        await Task.WhenAll(childNodesCopies).ConfigureAwait(false);
    }
}