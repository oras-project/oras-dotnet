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
    /// ExtendedCopyAsync copies the directed acyclic graph (DAG) that is reachable from
    /// the given tagged node from the source GraphTarget to the destination Target.
    /// In other words, it copies a tagged artifact along with its referrers or
    /// other predecessor manifests referencing it.
    /// The tagged node (e.g. a tagged manifest of the artifact) is identified by the
    /// source reference.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the tagged node on successful copy.
    /// </summary>
    /// <param name="src">
    /// The source read-only graph target from which the tagged node and its reachable DAG
    /// (including referrers and predecessor manifests) are copied.
    /// </param>
    /// <param name="srcRef">
    /// Source reference that identifies the tagged node in <paramref name="src"/>, typically a
    /// tag or digest (for example, <c>my-artifact:1.0</c> or <c>sha256:&lt;hex&gt;</c>).
    /// </param>
    /// <param name="dst">
    /// The destination target that will receive the copied content and tag for the tagged node.
    /// </param>
    /// <param name="dstRef">
    /// Destination reference to associate with the copied tagged node in <paramref name="dst"/>.
    /// If null or empty, the value of <paramref name="srcRef"/> is used.
    /// </param>
    /// <param name="opts">
    /// Options that control how the extended copy operation traverses and copies the graph,
    /// including behavior for referrers and predecessor manifests.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to observe for cancellation while resolving the source, copying the graph, or
    /// tagging the destination.
    /// </param>
    /// <returns>The descriptor of the tagged node after it has been copied to the destination.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="src"/>, <paramref name="dst"/>, or <paramref name="opts"/> is null,
    /// or when <paramref name="srcRef"/> is null or empty.
    /// </exception>
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
        if (string.IsNullOrEmpty(srcRef))
        {
            throw new ArgumentNullException(nameof(srcRef), "Source target reference cannot be null or empty");
        }
        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }
        if (opts == null)
        {
            throw new ArgumentNullException(nameof(opts), "ExtendedCopyOptions cannot be null");
        }

        var node = await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);

        await src.ExtendedCopyGraphAsync(dst, node, opts, cancellationToken).ConfigureAwait(false);

        await dst.TagAsync(node, dstRef, cancellationToken).ConfigureAwait(false);

        return node;
    }
}
