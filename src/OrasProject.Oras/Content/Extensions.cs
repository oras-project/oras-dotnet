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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

public static class Extensions
{
    /// <summary>
    /// Retrieves the successors of a node
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<Descriptor>> GetSuccessorsAsync(this IFetchable fetcher, Descriptor node, CancellationToken cancellationToken)
    {
        switch (node.MediaType)
        {
            case Docker.MediaType.Manifest:
            case Oci.MediaType.ImageManifest:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<Manifest>(content);
                    if (manifest == null)
                    {
                        throw new JsonException("null image manifest");
                    }
                    var descriptors = new List<Descriptor>() { manifest.Config };
                    descriptors.AddRange(manifest.Layers);
                    return descriptors;
                }
            case Docker.MediaType.ManifestList:
            case Oci.MediaType.ImageIndex:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    // docker manifest list and oci index are equivalent for successors.
                    var index = JsonSerializer.Deserialize<Oci.Index>(content);
                    if (index == null)
                    {
                        throw new JsonException("null image index");
                    }
                    return index.Manifests;
                }
        }
        return Array.Empty<Descriptor>();
    }

    /// <summary>
    /// Fetches all the content for a given descriptor.
    /// Currently only sha256 is supported but we would supports others hash algorithms in the future.
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="desc"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> FetchAllAsync(this IFetchable fetcher, Descriptor desc, CancellationToken cancellationToken)
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
    internal static async Task<byte[]> ReadAllAsync(this Stream stream, Descriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.Size < 0)
        {
            throw new InvalidDescriptorSizeException("this descriptor size is less than 0");
        }
        var buffer = new byte[descriptor.Size];
        try
        {
            await stream.ReadAsync(buffer, 0, (int)stream.Length, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException("this descriptor size is less than content size");
        }

        if (Digest.ComputeSHA256(buffer) != descriptor.Digest)
        {
            throw new MismatchedDigestException("this descriptor digest is different from content digest");
        }
        return buffer;
    }
}
