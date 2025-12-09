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

namespace OrasProject.Oras.Content;

public static class ReadOnlyGraphStorageExtensions
{
    /// <summary>
    /// ExtendedCopyGraphAsync copies the directed acyclic graph (DAG) that are reachable
    /// from the given node from the source GraphStorage to the destination Storage.
    /// In other words, it copies an artifact along with its referrers or other
    /// predecessor manifests referencing it.
    /// The node (e.g. a manifest of the artifact) is identified by a descriptor.
    /// </summary>
    /// <param name="src">The source graph storage</param>
    /// <param name="dst">The destination storage</param>
    /// <param name="node">The descriptor identifying the node to copy</param>
    /// <param name="opts">Options for the extended copy operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when src or dst is null</exception>
    public static async Task ExtendedCopyGraphAsync(
        this IReadOnlyGraphStorage src,
        IStorage dst,
        Descriptor node,
        ExtendedCopyGraphOptions opts,
        CancellationToken cancellationToken = default)
    {
        if (src == null)
        {
            throw new ArgumentNullException(nameof(src), "Source storage cannot be null");
        }
        if (dst == null)
        {
            throw new ArgumentNullException(nameof(dst), "Destination storage cannot be null");
        }

        var roots = await src.FindRootsAsync(node, opts, cancellationToken).ConfigureAwait(false);

        var proxy = new Proxy
        {
            Cache = new LimitedStorage(new MemoryStorage(), opts.MaxMetadataBytes),
            Source = src
        };

        // Use semaphore for concurrency control
        using var limiter = new SemaphoreSlim(opts.Concurrency, opts.Concurrency);

        // Copy the sub-DAGs rooted by the root nodes
        var copyTasks = new List<Task>();
        foreach (var root in roots)
        {
            copyTasks.Add(Task.Run(async () =>
            {
                // Copy the graph rooted at this root node
                await src.CopyGraphAsync(dst, root, proxy, opts, limiter, cancellationToken).ConfigureAwait(false);
            }, cancellationToken));
        }

        await Task.WhenAll(copyTasks).ConfigureAwait(false);
    }

    internal class NodeInfo
    {
        public Descriptor Node { get; set; } = default!;
        public int Depth { get; set; }
    }

    // findRoots finds the root nodes reachable from the given node through a
    // depth-first search.
    internal static async Task<List<Descriptor>> FindRootsAsync(
        this IReadOnlyGraphStorage src,
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

        opts.FindPredecessors ??= async (src, descriptor, cancellationToken) =>
        {
            return await src.GetPredecessorsAsync(descriptor, cancellationToken).ConfigureAwait(false);
        };

        var stack = new Stack<NodeInfo>();

        // push the initial node to the stack, set the depth to 0
        stack.Push(new NodeInfo { Node = node, Depth = 0 });
        while (stack.TryPop(out var current))
        {
            var currentNode = current.Node;
            var currentKey = currentNode.BasicDescriptor;
            if (visited.Contains(currentKey))
            {
                continue;
            }
            visited.Add(currentKey);

            if (opts.Depth > 0 && current.Depth == opts.Depth)
            {
                AddRoot(currentKey, currentNode);
                continue;
            }

            IEnumerable<Descriptor> predecessors;
            try
            {
                predecessors = await opts.FindPredecessors(src, currentNode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("FindPredecessors operation failed", ex);
            }

            var predecessorList = predecessors.ToList();

            if (predecessorList.Count == 0)
            {
                AddRoot(currentKey, currentNode);
                continue;
            }

            foreach (var predecessor in predecessorList)
            {
                var predecessorKey = predecessor.BasicDescriptor;
                if (!visited.Contains(predecessorKey))
                {
                    stack.Push(new NodeInfo { Node = predecessor, Depth = current.Depth + 1 });
                }
            }
        }

        var roots = new List<Descriptor>(rootMap.Values);
        return roots;
    }
}
