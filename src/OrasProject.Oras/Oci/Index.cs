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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Index is a higher-level manifest which points to specific image manifests, ideal for one or more platforms.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/image-index.md
/// </summary>
public class Index : Versioned
{
    // MediaType specifies the type of this document data structure e.g. `application/vnd.oci.image.index.v1+json`
    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? MediaType { get; set; }

    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ArtifactType { get; set; }

    // Manifests references platform specific manifests.
    [JsonPropertyName("manifests")]
    public required IList<Descriptor> Manifests { get; set; }

    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Descriptor? Subject { get; set; }

    // Annotations contains arbitrary metadata for the image index.
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IDictionary<string, string>? Annotations { get; set; }

    public Index() { }

    [SetsRequiredMembers]
    public Index(IList<Descriptor> manifests)
    {
        Manifests = manifests;
        MediaType = Oci.MediaType.ImageIndex;
        SchemaVersion = 2;
    }
}
