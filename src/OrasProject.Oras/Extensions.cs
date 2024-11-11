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

using System;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;
using static OrasProject.Oras.Content.Extensions;

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
    public static Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef,
        CancellationToken cancellationToken = default)
    {
        return src.CopyAsync(srcRef, dst, dstRef, new CopyGraphOptions(), cancellationToken);
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
    /// <param name="copyOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef, CopyGraphOptions? copyOptions = default, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }
        var root = await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);
        await src.CopyGraphAsync(dst, root, copyOptions, cancellationToken).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    public static Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, CancellationToken cancellationToken = default)
    {
        return src.CopyGraphAsync(dst, node, new CopyGraphOptions(), cancellationToken);
    }

    public static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, CopyGraphOptions? copyOptions = default, CancellationToken cancellationToken = default)
    {
        // check if node exists in target
        if (await dst.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
        {
            copyOptions?.OnCopySkipped(node);
            return;
        }

        // retrieve successors
        var successors = await src.GetSuccessorsAsync(node, cancellationToken).ConfigureAwait(false);

        // check if the node has successors
        foreach (var childNode in successors)
        {
            await src.CopyGraphAsync(dst, childNode, copyOptions, cancellationToken).ConfigureAwait(false);
        }

        // perform the copy
        copyOptions?.OnPreCopy(node);
        var dataStream = await src.FetchAsync(node, cancellationToken).ConfigureAwait(false);
        await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
        
        // we copied it
        copyOptions?.OnPostCopy(node);
    }
}

