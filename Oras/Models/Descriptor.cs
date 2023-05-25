using System.Collections.Generic;
using System.Text.Json.Serialization;


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

        internal MinimumDescriptor GetMinimum()
        {
            return new MinimumDescriptor
            {
                MediaType = this.MediaType,
                Digest = this.Digest,
                Size = this.Size
            };
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
