using Oras.Exceptions;
using Oras.Interfaces;
using Oras.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Content
{
    public static class StorageUtility
    {
        public static async Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var stream = await fetcher.FetchAsync(desc, cancellationToken);
            return await ReadAllAsync(stream, desc);
        }

        public static string CalculateHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var output = $"{nameof(SHA256)}:{BitConverter.ToString(hash).Replace("-", "")}";
            return output;
        }

        public static async Task<byte[]> ReadAllAsync(Stream stream, Descriptor descriptor)
        {
            if (descriptor.Size < 0)
            {
                throw new InvalidDescriptorSizeException("this descriptor size is less than 0");
            }
            var buffer = new byte[descriptor.Size];
            try
            {
                await stream.ReadAsync(buffer, 0, (int)stream.Length);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidDescriptorSizeException("this descriptor size is less than content size");
            }

            if (CalculateHash(buffer) != descriptor.Digest)
            {
                throw new MismatchedDigestException("this descriptor digest is different from content digest");
            }
            return buffer;
        }
    }
}
