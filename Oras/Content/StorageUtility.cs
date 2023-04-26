using Oras.Interfaces;
using Oras.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Content
{
    public static class StorageUtility
    {
        public static async Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var stream = await fetcher.FetchAsync(desc, cancellationToken);
            var bytes = new byte[stream.Length];
            // ought to implement a readall function to handle verification of the stream
            await stream.ReadAsync(bytes, 0, (int)stream.Length, cancellationToken);
            return bytes;
        }
    }
}

