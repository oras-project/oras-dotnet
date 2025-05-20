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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Registry;

namespace OrasProject.Oras;

public static class Extensions
{
    
    private static readonly SemaphoreSlim _semaphore = new(1, 3);

    public class Proxy : IFetchable
    {
        public required IStorage MemoryStorage { get; set; }
        public required ITarget Target { get; set; }

        public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            if (await MemoryStorage.ExistsAsync(target, cancellationToken).ConfigureAwait(false))
            {
                return await MemoryStorage.FetchAsync(target, cancellationToken).ConfigureAwait(false);
            }
            
            var manifest = await Target.FetchAsync(target, cancellationToken).ConfigureAwait(false);
            await MemoryStorage.PushAsync(target, manifest, cancellationToken).ConfigureAwait(false);
            return await MemoryStorage.FetchAsync(target, cancellationToken).ConfigureAwait(false);
        }
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
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken = default)
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
            MemoryStorage = new MemoryStorage(),
            Target = src
        };
        if (Descriptor.IsManifestType(root))
        {
            if (!await proxy.MemoryStorage.ExistsAsync(root, cancellationToken).ConfigureAwait(false))
            {
                await proxy.MemoryStorage.PushAsync(root, rootStream, cancellationToken).ConfigureAwait(false);
            }
        }
        await src.CopyGraphAsync(dst, root, proxy, cancellationToken).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    public static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, Proxy proxy, CancellationToken cancellationToken)
    {
        // acquire lock to find successors of the current node
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Descriptor> successors;
        // check if node exists in target
        try
        {
            if (await dst.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            // fetch once
            successors = await proxy.GetSuccessorsAsync(node, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // release
            _semaphore.Release();
        }

        
        var childNodesCopies = new List<Task>();
        foreach (var childNode in successors)
        {
            childNodesCopies.Add(src.CopyGraphAsync(dst, childNode, proxy, cancellationToken));
        }
        await Task.WhenAll(childNodesCopies).ConfigureAwait(false);
        
        
        // obtain data stream 
        // fetch twice
        // acquire lock again to perform copy
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Stream dataStream;
            if (await proxy.MemoryStorage.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
            {
                dataStream = await proxy.MemoryStorage.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                dataStream = await src.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            }
            await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
