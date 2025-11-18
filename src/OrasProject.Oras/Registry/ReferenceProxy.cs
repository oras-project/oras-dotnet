using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry;

internal interface IReferenceStorage : IReadOnlyStorage, IReferenceFetchable
{
}

internal class ReferenceProxy : IReferenceFetchable
{
    public required IReferenceFetchable ReferenceFetcher { get; init; }
    public required Proxy Proxy { get; init; }
    public static ReferenceProxy Create(IReferenceStorage baseStorage, IStorage cache)
    {
        return new ReferenceProxy
        {
            ReferenceFetcher = baseStorage,
            Proxy = new Proxy
            {
                Cache = cache,
                Source = baseStorage
            }
        };
    }

    public async Task<(Descriptor, Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var (target, rc) = await ReferenceFetcher.FetchAsync(reference, cancellationToken).ConfigureAwait(false);

        // skip caching if the content already exists in cache
        if (await Proxy.Cache.ExistsAsync(target, cancellationToken).ConfigureAwait(false))
        {
            await rc.DisposeAsync().ConfigureAwait(false);
            return (target, await Proxy.Cache.FetchAsync(target, cancellationToken).ConfigureAwait(false));
        }

        // cache content while reading
        var cacheStream = new MemoryStream();
        try
        {
            await rc.CopyToAsync(cacheStream, cancellationToken).ConfigureAwait(false);
            await rc.DisposeAsync().ConfigureAwait(false);

            cacheStream.Position = 0;
            
            // Push to cache in background (fire and forget style similar to Go goroutine)
            _ = Task.Run(async () =>
            {
                try
                {
                    var cacheData = cacheStream.ToArray();
                    using var pushStream = new MemoryStream(cacheData);
                    await Proxy.Cache.PushAsync(target, pushStream, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Silently ignore cache push errors to maintain compatibility with Go behavior
                }
            }, CancellationToken.None);

            // Return a new stream from the cached data
            return (target, new MemoryStream(cacheStream.ToArray()));
        }
        catch
        {
            await cacheStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
