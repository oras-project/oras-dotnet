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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;

namespace OrasProject.Oras;

public static class TargetExtensions
{
    /// <summary>
    /// Copy copies a rooted directed acyclic graph (DAG) with the tagged root node
    /// in the source Target to the destination Target.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the root node on successful copy.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="srcRef"></param>
    /// <param name="dst"></param>
    /// <param name="dstRef"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken = default)
    {
        return await src.CopyAsync(srcRef, dst, dstRef, new CopyOptions(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copy copies a rooted directed acyclic graph (DAG) with the tagged root node
    /// in the source Target to the destination Target.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the root node on successful copy.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="srcRef"></param>
    /// <param name="dst"></param>
    /// <param name="dstRef"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="copyOptions"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef, CopyOptions copyOptions, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(srcRef))
        {
            throw new ArgumentNullException(nameof(srcRef));
        }

        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }

        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = src
        };

        var (root, _) = await proxy.FetchAsync(srcRef, cancellationToken).ConfigureAwait(false);
        await src.CopyGraphAsync(dst, root, proxy, copyOptions, cancellationToken).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node desc from src to dst 
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    public static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, CancellationToken cancellationToken)
    {
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = src
        };
        var copyGraphOptions = new CopyGraphOptions();
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
    public static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken)
    {
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
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
    internal static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken)
    {
        await src.CopyGraphAsync(dst, node, proxy, copyGraphOptions, new SemaphoreSlim(1, copyGraphOptions.MaxConcurrency), cancellationToken)
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
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, CopyGraphOptions copyGraphOptions, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        if (Descriptor.IsNullOrInvalid(node))
        {
            throw new ArgumentNullException(nameof(node));
        }

        // acquire lock to find successors of the current node
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Descriptor> successors;
        try
        {
            // check if node exists in target
            if (await dst.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            successors = await proxy.GetSuccessorsAsync(node, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            limiter.Release();
        }

        var childNodesCopies = new List<Task>();
        foreach (var childNode in successors)
        {
            childNodesCopies.Add(Task.Run(async () =>
                    await src.CopyGraphAsync(dst, childNode, proxy, copyGraphOptions, limiter, cancellationToken).ConfigureAwait(false), cancellationToken));
        }
        await Task.WhenAll(childNodesCopies).ConfigureAwait(false);

        // acquire lock again to perform copy
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // obtain datastream
            using var dataStream = await proxy.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            limiter.Release();
        }
    }
}
