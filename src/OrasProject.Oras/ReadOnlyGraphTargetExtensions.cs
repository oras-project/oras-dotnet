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
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

public static class ReadOnlyGraphTargetExtensions
{
    /// <summary>
    /// ExtendedCopyAsync copies the directed acyclic graph (DAG) that are reachable from
    /// the given tagged node from the source GraphTarget to the destination Target.
    /// In other words, it copies a tagged artifact along with its referrers or
    /// other predecessor manifests referencing it.
    /// The tagged node (e.g. a tagged manifest of the artifact) is identified by the
    /// source reference.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the tagged node on successful copy.
    /// </summary>
    /// <param name="src">The source read-only graph target</param>
    /// <param name="srcRef">The source reference</param>
    /// <param name="dst">The destination target</param>
    /// <param name="dstRef">The destination reference (defaults to srcRef if null or empty)</param>
    /// <param name="opts">Options for the extended copy operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The descriptor of the tagged node</returns>
    /// <exception cref="ArgumentNullException">Thrown when src or dst is null</exception>
    public static async Task<Descriptor> ExtendedCopyAsync(
        this IReadOnlyGraphTarget src,
        string srcRef,
        ITarget dst,
        string dstRef,
        ExtendedCopyOptions opts,
        CancellationToken cancellationToken = default)
    {
        if (src == null)
        {
            throw new ArgumentNullException(nameof(src), "Source target cannot be null");
        }
        if (dst == null)
        {
            throw new ArgumentNullException(nameof(dst), "Destination target cannot be null");
        }

        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }

        var node = await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);

        await src.ExtendedCopyGraphAsync(dst, node, opts, cancellationToken).ConfigureAwait(false);

        await dst.TagAsync(node, dstRef, cancellationToken).ConfigureAwait(false);

        return node;
    }
}
