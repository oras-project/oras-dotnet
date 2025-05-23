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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Registry;

namespace OrasProject.Oras;

public static class Extensions
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
        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }

        Descriptor root;
        Stream rootStream;
        
        if (src is IReferenceFetchable srcRefFetchable)
        {
            (root, rootStream) = await srcRefFetchable.FetchAsync(srcRef, cancellationToken).ConfigureAwait(false);
        } else
        {
            root = await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);
            rootStream = await src.FetchAsync(root, cancellationToken).ConfigureAwait(false);
        }
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = src
        };
        if (Descriptor.IsManifestType(root))
        {
            if (!await proxy.Cache.ExistsAsync(root, cancellationToken).ConfigureAwait(false))
            {
                await proxy.Cache.PushAsync(root, rootStream, cancellationToken).ConfigureAwait(false);
            }
        }
        await src.CopyGraphAsync(dst, root, proxy, copyOptions, cancellationToken).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using proxy cache
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="proxy"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, CancellationToken cancellationToken)
    {
        await src.CopyGraphAsync(dst, node, proxy, new CopyGraphOptions(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using proxy cache
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="node"></param>
    /// <param name="proxy"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken)
    {
        // acquire lock to find successors of the current node
        await copyGraphOptions.SemaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            copyGraphOptions.SemaphoreSlim.Release();
        }
        
        var childNodesCopies = new List<Task>();
        foreach (var childNode in successors)
        {
            childNodesCopies.Add(Task.Run(async () => 
                    await src.CopyGraphAsync(dst, childNode, proxy, copyGraphOptions, cancellationToken).ConfigureAwait(false), cancellationToken));
        }
        await Task.WhenAll(childNodesCopies).ConfigureAwait(false);
        
        // acquire lock again to perform copy
        await copyGraphOptions.SemaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // obtain datastream
            Stream dataStream;
            if (await proxy.Cache.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
            {
                dataStream = await proxy.Cache.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                dataStream = await src.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            }
            await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            copyGraphOptions.SemaphoreSlim.Release();
        }
    }
}
