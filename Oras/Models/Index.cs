using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Oras.Models
{
    internal class Index
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        // MediaType specifies the type of this document data structure e.g. `application/vnd.oci.image.index.v1+json`
        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public string MediaType { get; set; }

        // Manifests references platform specific manifests.
        [JsonPropertyName("manifests")]
        public List<Descriptor> Manifests { get; set; }

        // Annotations contains arbitrary metadata for the image index.
        [JsonPropertyName("annotations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public Dictionary<string, string> Annotations { get; set; }
    }
}
