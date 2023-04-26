using Oras.Content;
using Oras.Interfaces;
using Oras.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Oras
{
    public class Copy
    {
        // Copy copies a rooted directed acyclic graph (DAG) with the tagged root node
        // in the source Target to the destination Target.
        // The destination reference will be the same as the source reference if the
        // destination reference is left blank.
        // Returns the descriptor of the root node on successful copy.
        public async Task<Descriptor> CopyAsync(ITarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken)
        {
            if (src is null)
            {
                throw new Exception("nil source target");
            }
            if (dst is null)
            {
                throw new Exception("nil destination target");
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
                var successors = await StorageUtility.SuccessorsAsync(src, node, cancellationToken);
                // obtain data stream
                var dataStream = await src.FetchAsync(node, cancellationToken);
                // check if the node has children
                if (successors != null)
                {
                    foreach (var childNode in successors)
                    {
                        await CopyGraphAsync(src, dst, childNode, cancellationToken);
                    }
                    await dst.PushAsync(node, dataStream, cancellationToken);
                }
                else
                {
                    await dst.PushAsync(node, dataStream, cancellationToken);
                }
            }
        }
    }
}
