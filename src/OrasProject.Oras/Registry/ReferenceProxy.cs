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
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Represents a CAS that supports <see cref="IReferenceFetchable"/>.
/// </summary>
internal interface IReferenceStorage : IReadOnlyStorage, IReferenceFetchable
{
}

/// <summary>
/// ReferenceProxy is a caching proxy dedicated for <see cref="IReferenceFetchable"/>.
/// The first fetch call of a described content will read from the remote and cache the fetched content.
/// The subsequent fetch call will read from the local cache.
/// </summary>
internal class ReferenceProxy : IReferenceFetchable
{
    private IReferenceFetchable ReferenceFetcher { get; }

    private Proxy Proxy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceProxy"/> class with an existing <see cref="Proxy"/>
    /// and <see cref="IReferenceFetchable"/>.
    /// </summary>
    /// <param name="referenceFetcher">The reference fetcher to use</param>
    /// <param name="proxy">The CAS proxy to use for caching</param>
    public ReferenceProxy(IReferenceFetchable referenceFetcher, Proxy proxy)
    {
        ReferenceFetcher = referenceFetcher;
        Proxy = proxy;
    }

    /// <summary>
    /// Fetches the content identified by the reference from the remote and caches the fetched content.
    /// </summary>
    /// <param name="reference">The reference to fetch</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A tuple containing the descriptor and the content stream</returns>
    public async Task<(Descriptor, Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var (target, stream) = await ReferenceFetcher.FetchAsync(reference, cancellationToken).ConfigureAwait(false);
        if (await Proxy.Cache.ExistsAsync(target, cancellationToken).ConfigureAwait(false))
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return (target, stream);
        }
        return (target, await Proxy.CacheContent(target, stream, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Fetches the content identified by the descriptor from the cache.
    /// </summary>
    /// <param name="desc">The descriptor identifying the content to fetch</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A stream containing the content</returns>
    public async Task<Stream> FetchAsync(Descriptor desc, CancellationToken cancellationToken)
    {
        return await Proxy.FetchAsync(desc, cancellationToken).ConfigureAwait(false);
    }
}
