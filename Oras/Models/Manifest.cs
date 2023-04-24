using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Oras.Models
{
    public class Manifest
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MediaType { get; set; }

        [JsonPropertyName("config")]
        public Descriptor Config { get; set; }

        [JsonPropertyName("layers")]
        public List<Descriptor> Layers { get; set; }

        [JsonPropertyName("subject")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public Descriptor Subject { get; set; }

        [JsonPropertyName("annotations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public Dictionary<string, string> Annotations { get; set; }
    }
}
