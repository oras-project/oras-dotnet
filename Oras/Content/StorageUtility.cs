using Oras.Interfaces;
using Oras.Models;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;

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
        
        public static string CalculateHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var output = $"{nameof(SHA256)}:{BitConverter.ToString(hash).Replace("-", "")}";
            return output;
        }
    }
}

