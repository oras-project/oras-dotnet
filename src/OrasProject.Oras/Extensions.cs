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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using static OrasProject.Oras.Content.Extensions;

namespace OrasProject.Oras;

public struct CopyOptions
{
    // public int Concurrency { get; set; }

    public event Action<Descriptor> OnPreCopy;
    public event Action<Descriptor> OnPostCopy;
    public event Action<Descriptor> OnCopySkipped;
    public event Action<Descriptor, string> OnMounted;
    
    public Func<Descriptor, string[]> MountFrom { get; set; }

    internal void PreCopy(Descriptor descriptor)
    {
        OnPreCopy?.Invoke(descriptor);
    }

    internal void PostCopy(Descriptor descriptor)
    {
        OnPostCopy?.Invoke(descriptor);
    }

    internal void CopySkipped(Descriptor descriptor)
    {
        OnCopySkipped?.Invoke(descriptor);
    }

    internal void Mounted(Descriptor descriptor, string sourceRepository)
    {
        OnMounted?.Invoke(descriptor, sourceRepository);
    }
}
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
    public static async Task<Descriptor> CopyAsync(this ITarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken = default, CopyOptions? copyOptions = default)
    {
        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }
        var root = await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);
        await src.CopyGraphAsync(dst, root, cancellationToken, copyOptions).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    public static async Task CopyGraphAsync(this ITarget src, ITarget dst, Descriptor node, CancellationToken cancellationToken, CopyOptions? copyOptions = default)
    {
        // check if node exists in target
        if (await dst.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
        {
            copyOptions?.CopySkipped(node);
            return;
        }

        // retrieve successors
        var successors = await src.GetSuccessorsAsync(node, cancellationToken).ConfigureAwait(false);

        // check if the node has successors
        foreach (var childNode in successors)
        {
            await src.CopyGraphAsync(dst, childNode, cancellationToken, copyOptions).ConfigureAwait(false);
        }

        var sourceRepositories = copyOptions?.MountFrom(node) ?? [];
        if (dst is IMounter mounter && sourceRepositories.Length > 0)
        {
            for (var i = 0; i < sourceRepositories.Length; i++)
            {
                var sourceRepository = sourceRepositories[i];
                var mountFailed = false;

                async Task<Stream> GetContents(CancellationToken token)
                {
                    // the invocation of getContent indicates that mounting has failed
                    mountFailed = true;

                    if (i < sourceRepositories.Length - 1)
                    {
                        // If this is not the last one, skip this source and try next one
                        // We want to return an error that we will test for from mounter.Mount()
                        throw new SkipSourceException();
                    }

                    // this is the last iteration so we need to actually get the content and do the copy
                    // but first call the PreCopy function
                    copyOptions?.PreCopy(node);
                    return await src.FetchAsync(node, token).ConfigureAwait(false);
                }

                try
                {
                    await mounter.MountAsync(node, sourceRepository, GetContents, cancellationToken).ConfigureAwait(false);
                }
                catch (SkipSourceException)
                {
                }

                if (!mountFailed)
                {
                    copyOptions?.Mounted(node, sourceRepository);
                    return;
                }
            }
        }
        else
        {
            // alternatively we just copy it
            copyOptions?.PreCopy(node);
            var dataStream = await src.FetchAsync(node, cancellationToken).ConfigureAwait(false);
            await dst.PushAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
        }
        
        // we copied it
        copyOptions?.PostCopy(node);
    }

    private class SkipSourceException : Exception {}
}

