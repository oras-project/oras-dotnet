using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Exceptions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrasProject.Oras;

[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(Index))]
[JsonSerializable(typeof(RepositoryList))]
[JsonSerializable(typeof(TagList))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(Reference))]
[JsonSerializable(typeof(JsonElement))]
internal partial class OrasJsonJsonSerializerContext : JsonSerializerContext
{
}
