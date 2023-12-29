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

using OrasProject.Oras.Interfaces;
using OrasProject.Oras.Oci;
using System;
using System.Threading;
using System.Threading.Tasks;
using static OrasProject.Oras.Content.Content;

namespace OrasProject.Oras
{
    public class Copy
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
        public static async Task<Descriptor> CopyAsync(ITarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken)
        {
            if (src is null)
            {
                throw new Exception("null source target");
            }
            if (dst is null)
            {
                throw new Exception("null destination target");
            }
            if (dstRef == string.Empty)
            {
                dstRef = srcRef;
            }
            var root = await src.ResolveAsync(srcRef, cancellationToken);
            await CopyGraphAsync(src, dst, root, cancellationToken);
            await dst.TagAsync(root, dstRef, cancellationToken);
            return root;
        }

        public static async Task CopyGraphAsync(ITarget src, ITarget dst, Descriptor node, CancellationToken cancellationToken)
        {
            // check if node exists in target
            if (!await dst.ExistsAsync(node, cancellationToken))
            {
                // retrieve successors
                var successors = await SuccessorsAsync(src, node, cancellationToken);
                // obtain data stream
                var dataStream = await src.FetchAsync(node, cancellationToken);
                // check if the node has successors
                if (successors != null)
                {
                    foreach (var childNode in successors)
                    {
                        await CopyGraphAsync(src, dst, childNode, cancellationToken);
                    }
                }
                await dst.PushAsync(node, dataStream, cancellationToken);
            }
        }
    }
}
