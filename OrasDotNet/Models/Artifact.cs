using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace OrasDotnet.Models
{
    public class Artifact
    {
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }

        [JsonPropertyName("artifactType")]
        public string ArtifactType { get; set; }

        [JsonPropertyName("blobs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public List<Descriptor> Blobs { get; set; }

        [JsonPropertyName("subject")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public Descriptor Subject { get; set; }

        [JsonPropertyName("annotations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public Dictionary<string, string> Annotations { get; set; }
    }
}
