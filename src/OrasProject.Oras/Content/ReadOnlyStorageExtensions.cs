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
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

public static class ReadOnlyStorageExtensions
{
    /// <summary>
    /// CopyGraphAsync concurrently copy node desc from src to dst 
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    public static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor node, CancellationToken cancellationToken = default)
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
    public static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor node, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken = default)
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
    internal static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor node, Proxy proxy, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken = default)
    {
        using var limiter = new SemaphoreSlim(copyGraphOptions.Concurrency, copyGraphOptions.Concurrency);
        var copied = new ConcurrentDictionary<BasicDescriptor, bool>();
        await src.CopyGraphAsync(dst, node, proxy, copyGraphOptions, limiter, copied, cancellationToken).ConfigureAwait(false);
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
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor node, Proxy proxy, CopyGraphOptions copyGraphOptions, SemaphoreSlim limiter, ConcurrentDictionary<BasicDescriptor, bool> copied, CancellationToken cancellationToken)
    {
        if (Descriptor.IsNullOrInvalid(node))
        {
            throw new ArgumentNullException(nameof(node));
        }

        var nodeKey = node.BasicDescriptor;

        // Try to mark this node as being copied; skip if already claimed by another task
        if (!copied.TryAdd(nodeKey, true))
        {
            // Another task is already copying this node, skip it
            if (copyGraphOptions.OnCopySkippedAsync != null)
            {
                await copyGraphOptions.OnCopySkippedAsync(node, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        // acquire lock to find successors of the current node
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Descriptor> successors;
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
            successors = await copyGraphOptions.FindSuccessorsAsync(proxy, node, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            limiter.Release();
        }

        var childNodesCopies = new List<Task>();
        foreach (var childNode in successors)
        {
            childNodesCopies.Add(Task.Run(() => src.CopyGraphAsync(dst, childNode, proxy, copyGraphOptions, limiter, copied, cancellationToken)));
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
}
