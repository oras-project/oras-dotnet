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
    internal class NodeInfo
    {
        public Descriptor Node { get; set; } = default!;
        public int Depth { get; set; }
    }

    //  src should be IReadOnlyGraphStorage, use ITarget here for quick dev
    internal static async Task<List<Descriptor>> FindRootsAsync(
        this ITarget src,
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

    // TODO: generate summary
    // TODO: src should be IReadOnlyGraphStorage, use ITarget here for quick dev
    internal static async Task ExtendedCopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, ExtendedCopyGraphOptions extendedCopyGraphOptions, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        // check src, check dst
        // findroots
        // check concurrency, limiter, maxBytes
        // create proxy & tracker
        // copyGraph on roots

        var roots = await FindRootsAsync(src, node, extendedCopyGraphOptions, cancellationToken).ConfigureAwait(false);

        var rootCopies = new List<Task>();
        foreach (var root in roots)
        {
            rootCopies.Add(Task.Run(async () =>
                    await src.CopyGraphAsync(dst, root, proxy, extendedCopyGraphOptions, limiter, cancellationToken).ConfigureAwait(false), cancellationToken));
        }
        await Task.WhenAll(rootCopies).ConfigureAwait(false);
    }
}