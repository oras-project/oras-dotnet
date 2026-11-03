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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

/// <summary>
/// ReadOnlyStorageExtensions provides extension helpers for read-only storage.
/// </summary>
public static class ReadOnlyStorageExtensions
{
    /// <summary>
    /// CopyGraphAsync concurrently copy node desc from src to dst 
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException">The dependency graph contains a cycle.</exception>
    public static async Task CopyGraphAsync(
        this IReadOnlyStorage src,
        IStorage dst,
        Descriptor node,
        CancellationToken cancellationToken = default)
    {
        var copyGraphOptions = new CopyGraphOptions();
        var proxy = new Proxy()
        {
            Cache = new LimitedStorage(new MemoryStorage(), copyGraphOptions.MaxMetadataBytes),
            Source = src
        };
        await src.CopyGraphAsync(dst, node, proxy, copyGraphOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using customized copyGraphOptions
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException">The dependency graph contains a cycle.</exception>
    public static async Task CopyGraphAsync(
        this IReadOnlyStorage src,
        IStorage dst,
        Descriptor node,
        CopyGraphOptions copyGraphOptions,
        CancellationToken cancellationToken = default)
    {
        var proxy = new Proxy()
        {
            Cache = new LimitedStorage(new MemoryStorage(), copyGraphOptions.MaxMetadataBytes),
            Source = src
        };
        await src.CopyGraphAsync(dst, node, proxy, copyGraphOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using customized copyGraphOptions and cached proxy
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="proxy"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(
        this IReadOnlyStorage src,
        IStorage dst,
        Descriptor node,
        Proxy proxy,
        CopyGraphOptions copyGraphOptions,
        CancellationToken cancellationToken = default)
    {
        using var limiter = new SemaphoreSlim(copyGraphOptions.Concurrency, copyGraphOptions.Concurrency);
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();
        var parents = new Dictionary<BasicDescriptor, List<BasicDescriptor>>();
        await src.CopyGraphAsync(dst, node, proxy, copyGraphOptions, limiter, copied, parents, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using proxy cache with copyGraphOptions and SemaphoreSlim
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="proxy"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="limiter"></param>
    /// <param name="copied"></param>
    /// <param name="parents"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(
        this IReadOnlyStorage src,
        IStorage dst,
        Descriptor node,
        Proxy proxy,
        CopyGraphOptions copyGraphOptions,
        SemaphoreSlim limiter,
        ConcurrentDictionary<BasicDescriptor, Lazy<Task>> copied,
        Dictionary<BasicDescriptor, List<BasicDescriptor>> parents,
        CancellationToken cancellationToken)
    {
        if (Descriptor.IsNullOrInvalid(node))
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Think of this as "start this copy when requested, exactly once, and share its Task."
        //
        // - () => CopyNodeAsync(...) is a recipe for starting the copy. Creating the
        //   Lazy wrapper does not call it yet.
        // - Lazy<Task> runs that recipe on the first access to .Value, then stores
        //   the returned Task.
        // - ExecutionAndPublication ensures that even if multiple threads access .Value
        //   simultaneously, only one executes the recipe; everyone receives the same Task.
        //
        // For a shared layer, GetOrAdd below gives both parent traversals the same Lazy<Task>.
        // The first .Value access starts its copy; both parents then await that one operation.
        // Other Lazy wrappers created for the same layer are discarded without starting a copy.
        //
        // Important distinction: Lazy controls creating the Task, not waiting for the upload
        // to finish. Initialization finishes when the Task is returned; the await below waits
        // for actual copy completion.
        var copy = new Lazy<Task>(
            () => src.CopyNodeAsync(
                dst, node, proxy, copyGraphOptions, limiter, copied, parents, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var sharedCopy = copied.GetOrAdd(node.BasicDescriptor, copy);
        await sharedCopy.Value.ConfigureAwait(false);
        if (!ReferenceEquals(copy, sharedCopy) && copyGraphOptions.OnCopySkippedAsync != null)
        {
            await copyGraphOptions.OnCopySkippedAsync(node, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task CopyNodeAsync(
        this IReadOnlyStorage src,
        IStorage dst,
        Descriptor node,
        Proxy proxy,
        CopyGraphOptions copyGraphOptions,
        SemaphoreSlim limiter,
        ConcurrentDictionary<BasicDescriptor, Lazy<Task>> copied,
        Dictionary<BasicDescriptor, List<BasicDescriptor>> parents,
        CancellationToken cancellationToken)
    {
        // acquire lock to find successors of the current node
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        List<Descriptor> successors;
        try
        {
            // check if node exists in target
            if (await dst.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
            {
                if (copyGraphOptions.OnCopySkippedAsync != null)
                {
                    await copyGraphOptions.OnCopySkippedAsync(node, cancellationToken).ConfigureAwait(false);
                }
                return;
            }
            successors = (await copyGraphOptions.FindSuccessorsAsync(proxy, node, cancellationToken)
                .ConfigureAwait(false)).ToList();
        }
        finally
        {
            limiter.Release();
        }

        RegisterSuccessors(node, successors, parents);
        var childNodesCopies = new List<Task>();
        foreach (var childNode in successors)
        {
            childNodesCopies.Add(Task.Run(
                () => src.CopyGraphAsync(
                    dst, childNode, proxy, copyGraphOptions, limiter, copied, parents, cancellationToken),
                cancellationToken));
        }
        await Task.WhenAll(childNodesCopies).ConfigureAwait(false);

        // acquire lock again to perform copy
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (copyGraphOptions.PreCopyAsync != null)
            {
                var preDecision = await copyGraphOptions.PreCopyAsync(node, cancellationToken).ConfigureAwait(false);
                if (preDecision == CopyNodeDecision.SkipNode)
                {
                    return;
                }
            }

            // obtain datastream
            using var dataStream = await proxy.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
            if (copyGraphOptions.PostCopyAsync != null)
            {
                await copyGraphOptions.PostCopyAsync(node, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            limiter.Release();
        }
    }

    private static void RegisterSuccessors(
        Descriptor node,
        List<Descriptor> successors,
        Dictionary<BasicDescriptor, List<BasicDescriptor>> parents)
    {
        var successorKeys = new HashSet<BasicDescriptor>();
        foreach (var successor in successors)
        {
            if (Descriptor.IsNullOrInvalid(successor))
            {
                throw new ArgumentNullException(nameof(node));
            }
            successorKeys.Add(successor.BasicDescriptor);
        }
        if (successorKeys.Count == 0)
        {
            return;
        }

        // Copies of A and B can start independently:
        //
        //     Copy A: A needs B, so it waits for B's copy.
        //     Copy B: B needs A, so it waits for A's copy.
        //
        // Checking each copy's own ancestry misses the problem:
        // A -> B and B -> A contain no repeated nodes individually.
        // But together they form a cycle:
        //
        //     A ----> B
        //     ^       |
        //     |_______|
        //
        // Both copies would wait forever for each other.
        //
        // Keep each node's known parents in one shared graph: for A -> B, record A as B's parent.
        // Before adding new dependencies, walk this node's ancestors, not its shared descendants.
        // If a dependency is this node or one of its ancestors, the new edge would close a cycle.
        // Entirely new dependencies cannot lead back to this node, so skip their ancestry scan.
        // Reject cycles before starting dependency waits. Leaf nodes have no edges to check.
        // Check and record under the same lock so concurrent copies cannot both miss the cycle.
        lock (parents)
        {
            var key = node.BasicDescriptor;
            var pending = new Stack<BasicDescriptor>();
            if (successorKeys.Contains(key) || successorKeys.Any(parents.ContainsKey))
            {
                pending.Push(key);
            }
            var visited = new HashSet<BasicDescriptor>();
            while (pending.TryPop(out var ancestor))
            {
                if (!visited.Add(ancestor))
                {
                    continue;
                }
                if (successorKeys.Contains(ancestor))
                {
                    throw new InvalidOperationException(
                        $"Dependency cycle detected while copying {node.Digest}.");
                }
                if (parents.TryGetValue(ancestor, out var ancestorParents))
                {
                    foreach (var parent in ancestorParents)
                    {
                        pending.Push(parent);
                    }
                }
            }
            if (!parents.ContainsKey(key))
            {
                parents.Add(key, new List<BasicDescriptor>());
            }
            foreach (var successor in successorKeys)
            {
                if (!parents.TryGetValue(successor, out var successorParents))
                {
                    successorParents = new List<BasicDescriptor>();
                    parents.Add(successor, successorParents);
                }
                successorParents.Add(key);
            }
        }
    }
}
