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

using OrasProject.Oras.Constants;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Interfaces;
using OrasProject.Oras.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Index = OrasProject.Oras.Models.Index;

namespace OrasProject.Oras.Content
{
    public static class Content
    {
        /// <summary>
        /// Retrieves the successors of a node
        /// </summary>
        /// <param name="fetcher"></param>
        /// <param name="node"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Fetches all the content for a given descriptor.
        /// Currently only sha256 is supported but we would supports others hash algorithms in the future.
        /// </summary>
        /// <param name="fetcher"></param>
        /// <param name="desc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var stream = await fetcher.FetchAsync(desc, cancellationToken);
            return await ReadAllAsync(stream, desc);
        }

        /// <summary>
        /// Calculates the digest of the content
        /// Currently only sha256 is supported but we would supports others hash algorithms in the future.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        internal static string CalculateDigest(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var output = $"{nameof(SHA256)}:{BitConverter.ToString(hash).Replace("-", "")}";
            return output.ToLower();
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
        internal static async Task<byte[]> ReadAllAsync(Stream stream, Descriptor descriptor)
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
