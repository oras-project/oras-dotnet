using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace Oras.Models
{
    public class Descriptor : IEquatable<Descriptor>
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

        internal static MinimumDescriptor FromOCI(Descriptor descriptor)
        {
            return new MinimumDescriptor
            {
                MediaType = descriptor.MediaType,
                Digest = descriptor.Digest,
                Size = descriptor.Size
            };
        }

        public bool Equals(Descriptor other)
        {
            if (other == null) return false;
            return this.MediaType == other.MediaType && this.Digest == other.Digest && this.Size == other.Size;
        }



        public override int GetHashCode()
        {
            return HashCode.Combine(MediaType, Digest, Size, URLs, Annotations, Data, Platform, ArtifactType);
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
