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
    public class StorageUtility
    {
       public static async Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var t = await fetcher.FetchAsync(desc, cancellationToken);
            var tempByte = new byte[t.Length];
            // ought to implement a readall function to handle verification of the stream
            t.Read(tempByte, 0, (int)t.Length);
            return tempByte;
        }
       public static async Task<IList<Descriptor>> SuccessorsAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
            switch (node.MediaType)
            {
                case DockerMediaTypes.Manifest:
                    {
                        // OCI manifest schema can be used to marshal docker manifest
                        var dockerManifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor> { dockerManifest.Config }.Concat(dockerManifest.Layers).ToList();
                        return descriptors;
                    }
                case OCIMediaTypes.ImageManifest:
                    {
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
                        // docker manifest list and oci _index are equivalent for successors.
                        var index = JsonSerializer.Deserialize<Index>(content);
                        return index.Manifests;
                    }
            }
            return default;
        }
    }
}