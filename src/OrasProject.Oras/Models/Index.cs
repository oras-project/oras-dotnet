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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Models
{
    public class Index
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
