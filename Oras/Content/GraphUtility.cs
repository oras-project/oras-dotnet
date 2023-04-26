using Oras.Constants;
using Oras.Interfaces;
using Oras.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Index = Oras.Models.Index;

namespace Oras.Content
{
    public static class GraphUtility
    {
        public static async Task<IList<Descriptor>> SuccessorsAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            switch (node.MediaType)
            {
                case DockerMediaTypes.Manifest:
                case OCIMediaTypes.ImageManifest:
                    {
                        var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
                        var manifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor>() { manifest.Config };
                        descriptors.AddRange(manifest.Layers);
                        return descriptors;
                    }
                case DockerMediaTypes.ManifestList:
                case OCIMediaTypes.ImageIndex:
                    {
                        var content = await StorageUtility.FetchAllAsync(fetcher, node, cancellationToken);
                        // docker manifest list and oci index are equivalent for successors.
                        var index = JsonSerializer.Deserialize<Index>(content);
                        return index.Manifests;
                    }
            }
            return default;
        }

    }
}
