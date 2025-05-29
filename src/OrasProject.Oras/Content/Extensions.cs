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

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Index = OrasProject.Oras.Oci.Index;
namespace OrasProject.Oras.Content;


public static class Extensions
{
    /// <summary>
    /// GetSuccessorsAsync retrieves the successors of a node
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<Descriptor>> GetSuccessorsAsync(this IFetchable fetcher, Descriptor node, CancellationToken cancellationToken = default)
    {
        switch (node.MediaType)
        {
            case Docker.MediaType.Manifest:
            case MediaType.ImageManifest:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<Manifest>(content) ??
                                        throw new JsonException("Failed to deserialize manifest");
                    
                    var descriptors = new List<Descriptor>();
                    if (manifest.Subject != null)
                    {
                        // Note: Subject field only works for Oci Image Manifest
                        descriptors.Add(manifest.Subject);
                    }
                    descriptors.Add(manifest.Config);
                    descriptors.AddRange(manifest.Layers);
                    return descriptors;
                }
            case Docker.MediaType.ManifestList:
            case MediaType.ImageIndex:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    var index = JsonSerializer.Deserialize<Index>(content) ??
                                        throw new JsonException("Failed to deserialize manifest");
                    var descriptors = new List<Descriptor>();
                    if (index.Subject != null)
                    {
                        // Note: Subject field only works for Oci Index Manifest
                        descriptors.Add(index.Subject);
                    }
                    descriptors.AddRange(index.Manifests);
                    return descriptors;
                }
        }
        return [];
    }

    /// <summary>
    /// Fetches all the content for a given descriptor.
    /// Currently only sha256 is supported but we would supports others hash algorithms in the future.
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="desc"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> FetchAllAsync(this IFetchable fetcher, Descriptor desc, CancellationToken cancellationToken = default)
    {
        var stream = await fetcher.FetchAsync(desc, cancellationToken).ConfigureAwait(false);
        return await stream.ReadAllAsync(desc, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and verifies the content from a stream
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDescriptorSizeException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="MismatchedDigestException"></exception>
    public static async Task<byte[]> ReadAllAsync(this Stream stream, Descriptor descriptor, CancellationToken cancellationToken = default)
    {
        if (descriptor.Size < 0)
        {
            throw new InvalidDescriptorSizeException("Descriptor size is less than 0");
        }
        var buffer = new byte[descriptor.Size];
        try
        {
            await stream.ReadAsync(buffer.AsMemory(0, (int)stream.Length), cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new MismatchedDigestException("Descriptor size is less than content size");
        }

        if (Digest.ComputeSha256(buffer) != descriptor.Digest)
        {
            throw new MismatchedDigestException("Descriptor digest is different from content digest");
        }
        return buffer;
    }

    /// <summary>
    /// Reads the content from a stream up to a specified byte limit.
    /// Throws SizeLimitExceededException if the content exceeds the limit.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="maxBytes"></param>
    /// <exception cref="SizeLimitExceededException"></exception>
    internal static async Task<byte[]> ReadStreamWithLimitAsync(
        this Stream stream,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            long remaining = stream.Length - stream.Position;
            if (remaining > maxBytes)
                throw new SizeLimitExceededException($"Content size exceeds limit {maxBytes} bytes");
        }
        using var ms = new MemoryStream((int)Math.Min(maxBytes, 8192));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192); // 8 KB

        try
        {
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (totalRead > maxBytes - read)
                    throw new SizeLimitExceededException($"Content size exceeds limit {maxBytes} bytes.");
                ms.Write(buffer, 0, read);
                totalRead += read;
            }
            return ms.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
