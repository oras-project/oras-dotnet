using Oras.Constants;
using Oras.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace Oras.Models
{
    public class Descriptor
    {
        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MediaType { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("urls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IList<string> URLs { get; set; }

        [JsonPropertyName("annotations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IDictionary<string, string> Annotations { get; set; }

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public byte[] Data { get; set; }

        [JsonPropertyName("platform")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Platform Platform { get; set; }

        [JsonPropertyName("artifactType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ArtifactType { get; set; }

        public static MinimumDescriptor FromOCI(Descriptor descriptor)
        {
            return new MinimumDescriptor
            {
                MediaType = descriptor.MediaType,
                Digest = descriptor.Digest,
                Size = descriptor.Size
            };
    }

        async public Task<IList<Descriptor>> SuccessorsAsync(IFetcher fetcher, Descriptor node, CancellationToken cancellationToken)
        {
            var content = await FetchAllAsync(fetcher, node, cancellationToken);
            switch (node.MediaType)
            {
                case DockerMediaTypes.Manifest:
                    {
                        // OCI manifest schema can be used to marshal docker manifest
                        var dockerManifest = JsonSerializer.Deserialize<Manifest>(content);
                        var descriptors = new List<Descriptor> { dockerManifest.Config }.Concat(dockerManifest.Layers).ToList();
                        return descriptors;
                    }
                case OCISPECMediaTypes.ImageManifest:
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
                case OCISPECMediaTypes.ImageIndex:
                    {
                        // docker manifest list and oci index are equivalent for successors.
                        var index = JsonSerializer.Deserialize<Index>(content);
                        return index.Manifests;
                    }

                case OCISPECMediaTypes.ArtifactManifest:
                    {
                        var artifact = JsonSerializer.Deserialize<Artifact>(content);
                        var nodes = new List<Descriptor>();
                        if (artifact.Subject != null)
                        {
                            nodes.Add(artifact.Subject);
                        }
                        nodes.AddRange(artifact.Blobs);
                        return nodes;
                    }

            }
            return default;
        }

        async public Task<Byte[]> FetchAllAsync(IFetcher fetcher, Descriptor desc, CancellationToken cancellationToken)
        {
            var t = await fetcher.FetchAsync(desc, cancellationToken);
            var tempByte = new byte[t.Length];
            // ought to implement a readall function to handle verification of the stream
            t.Read(tempByte, 0, (int)t.Length);
            return tempByte;
        }
    }

    public class Platform
    {
        [JsonPropertyName("architecture")]
        public string Architecture { get; set; }

        [JsonPropertyName("os")]
        public string OS { get; set; }

        [JsonPropertyName("os.version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string OSVersion { get; set; }

        [JsonPropertyName("os.features")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IList<string> OSFeatures { get; set; }

        [JsonPropertyName("variant")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Variant { get; set; }
    }

}
