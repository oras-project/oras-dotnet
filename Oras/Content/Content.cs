using Oras.Constants;
using Oras.Interfaces;
using Oras.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Index = Oras.Models.Index;
using Oras.Exceptions;
using System.IO;
using System.Security.Cryptography;

namespace Oras.Content
{
    public static class Content
    {
        public static async Task<IList<Descriptor>> SuccessorsAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            switch (node.MediaType)
            {
                case DockerMediaTypes.Manifest:
                case OCIMediaTypes.ImageManifest:
                    {
                        var content = await FetchAllAsync(fetcher, node, cancellationToken);
                        var manifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor>() { manifest.Config };
                        descriptors.AddRange(manifest.Layers);
                        return descriptors;
                    }
                case DockerMediaTypes.ManifestList:
                case OCIMediaTypes.ImageIndex:
                    {
                        var content = await FetchAllAsync(fetcher, node, cancellationToken);
                        // docker manifest list and oci index are equivalent for successors.
                        var index = JsonSerializer.Deserialize<Index>(content);
                        return index.Manifests;
                    }
            }
            return default;
        }

        public static async Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var stream = await fetcher.FetchAsync(desc, cancellationToken);
            return await ReadAllAsync(stream, desc);
        }

        public static string CalculateDigest(byte[] content)
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
                throw new ArgumentOutOfRangeException("this descriptor size is less than content size");
            }

            if (CalculateDigest(buffer) != descriptor.Digest)
            {
                throw new MismatchedDigestException("this descriptor digest is different from content digest");
            }
            return buffer;
        }
    }
}
