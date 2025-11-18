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

namespace OrasProject.Oras.Content;

public static class TargetExtensions
{
    /// <summary>
    /// CopyGraphAsync concurrently copy node desc from src to dst 
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="root"></param>
    /// <param name="cancellationToken"></param>
    public static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor root, CancellationToken cancellationToken = default)
    {
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = src
        };
        var copyGraphOptions = new CopyGraphOptions();
        await src.CopyGraphAsync(dst, root, proxy, copyGraphOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using customized copyGraphOptions
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="root"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor root, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken = default)
    {
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = src
        };
        await src.CopyGraphAsync(dst, root, proxy, copyGraphOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using customized copyGraphOptions and cached proxy
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="root"></param>
    /// <param name="proxy"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor root, Proxy proxy, CopyGraphOptions copyGraphOptions, CancellationToken cancellationToken = default)
    {
        await src.CopyGraphAsync(dst, root, proxy, copyGraphOptions, new SemaphoreSlim(1, copyGraphOptions.MaxConcurrency), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// CopyGraphAsync concurrently copy node from src to dst by using proxy cache with copyGraphOptions and SemaphoreSlim
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="root"></param>
    /// <param name="proxy"></param>
    /// <param name="copyGraphOptions"></param>
    /// <param name="limiter"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyGraphAsync(this IReadOnlyStorage src, IStorage dst, Descriptor root, Proxy proxy, CopyGraphOptions copyGraphOptions, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        if (Descriptor.IsNullOrInvalid(root))
        {
            throw new ArgumentNullException(nameof(root));
        }

        // acquire lock to find successors of the current node
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Descriptor> successors;
        try
        {
            // check if node exists in target
            if (await dst.ExistsAsync(root, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            successors = await proxy.GetSuccessorsAsync(root, cancellationToken).ConfigureAwait(false);
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
            using var dataStream = await proxy.FetchAsync(root, cancellationToken).ConfigureAwait(false);
            await dst.PushAsync(root, dataStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            limiter.Release();
        }
    }
}
