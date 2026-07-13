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

using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using System.Text.Json;
using System.Text.Json.Serialization;
using static OrasProject.Oras.Registry.Remote.Registry;
using static OrasProject.Oras.Registry.Remote.Repository;

namespace OrasProject.Oras.Serialization;

[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(Index))]
[JsonSerializable(typeof(Descriptor))]
[JsonSerializable(typeof(Platform))]
[JsonSerializable(typeof(RepositoryList))]
[JsonSerializable(typeof(TagList))]
[JsonSerializable(typeof(Reference))]
[JsonSerializable(typeof(JsonElement))]
internal partial class OciJsonSerializerContext : JsonSerializerContext
{
    /// <summary>
    /// A pre-configured instance that applies Go-compatible string and
    /// dictionary encoding (same behaviour as the old JsonSerializerOptions).
    /// Use this instance for all OCI serialization / deserialization.
    /// </summary>
    internal static readonly OciJsonSerializerContext OciDefault =
        new OciJsonSerializerContext(new JsonSerializerOptions
        {
            Converters = { new OciStringConverter(), new OciDictionaryConverter() }
        });
}
