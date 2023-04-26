using Oras.Constants;
using Oras.Interfaces;
using Oras.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Index = Oras.Models.Index;

namespace Oras.Content
{
    public static class StorageUtility
    {
        public static async Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var stream = await fetcher.FetchAsync(desc, cancellationToken);
            var tempBytes = new byte[stream.Length];
            // ought to implement a readall function to handle verification of the stream
            await stream.ReadAsync(tempBytes, 0, (int)stream.Length, cancellationToken);
            return tempBytes;
        }
        public static async Task<IList<Descriptor>> SuccessorsAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            switch (node.MediaType)
            {
                case DockerMediaTypes.Manifest:
                    {
                        var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
                        // OCI manifest schema can be used to marshal docker manifest
                        var dockerManifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor> { dockerManifest.Config }.Concat(dockerManifest.Layers).ToList();
                        return descriptors;
                    }
                case OCIMediaTypes.ImageManifest:
                    {
                        var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
                        var manifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor>();
                        if (manifest.Subject != null)
                        {
                            descriptors.Add(manifest.Subject);
                        }
                        descriptors.Add(manifest.Config);
                        descriptors.AddRange(manifest.Layers);
                        return descriptors;
                    }
                case DockerMediaTypes.ManifestList:
                case OCIMediaTypes.ImageIndex:
                    {
                        var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
                        // docker manifest list and oci _index are equivalent for successors.
                        var index = JsonSerializer.Deserialize<Index>(content);
                        return index.Manifests;
                    }
            }
            return default;
        }
    }
}

