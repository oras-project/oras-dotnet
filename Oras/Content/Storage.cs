using Oras.Interfaces;
using Oras.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Oras.Content
{
    internal class Storage
    {
        async public static Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var t = await fetcher.FetchAsync(desc, cancellationToken);
            var tempByte = new byte[t.Length];
            // ought to implement a readall function to handle verification of the stream
            t.Read(tempByte, 0, (int)t.Length);
            return tempByte;
        }

    }
}
