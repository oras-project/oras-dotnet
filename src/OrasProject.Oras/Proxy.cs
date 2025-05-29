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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;

namespace OrasProject.Oras;

/// <summary>
/// Proxy class is to cache the manifest for OCI image/index manifest to improve performance
/// </summary>
internal class Proxy : IFetchable
{
    public required IStorage Cache { get; init; }
    public required ITarget Source { get; init; }

    /// <summary>
    /// FetchAsync is to fetch the content for the given target desc,
    /// if it is a cache hit, then return cached content
    /// otherwise, fetch the content, cache it if it is a manifest type, and return the content
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        if (await Cache.ExistsAsync(target, cancellationToken).ConfigureAwait(false))
        {
            return await Cache.FetchAsync(target, cancellationToken).ConfigureAwait(false);
        }
        
        var dataStream = await Source.FetchAsync(target, cancellationToken).ConfigureAwait(false);
        return await CacheContent(target, dataStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// FetchAsync fetch the descriptor and content stream via reference, cache the content if it is a manifest type
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor, Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        Descriptor node;
        Stream contentStream;
        
        if (Source is IReferenceFetchable srcRefFetchable)
        {
            (node, contentStream) = await srcRefFetchable.FetchAsync(reference, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            node = await Source.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
            contentStream = await Source.FetchAsync(node, cancellationToken).ConfigureAwait(false);
        }
        
        return (node, await CacheContent(node, contentStream, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// CacheContent caches the content if it is a manifest type
    /// </summary>
    /// <param name="node"></param>
    /// <param name="contentStream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<Stream> CacheContent(Descriptor node, Stream contentStream, CancellationToken cancellationToken = default)
    {
        if (!Descriptor.IsManifestType(node))
        {
            return contentStream;
        }
        
        try
        {
            // Caching index/image manifest is to reduce the number of requests
            // to retrieve image/index manifest by GetSuccessorsAsync and PushAsync in CopyGraphAsync
            await Cache.PushAsync(node, contentStream, cancellationToken).ConfigureAwait(false);
        }
        catch (AlreadyExistsException) { }
        finally
        {
            await contentStream.DisposeAsync().ConfigureAwait(false);
        }

        return await Cache.FetchAsync(node, cancellationToken).ConfigureAwait(false);
    }
}
