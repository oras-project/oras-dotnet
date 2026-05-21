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

using System.Text;
using System.Text.Json;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;
using OciIndex = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Serialization;

public partial class ManifestSerializationTest
{
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{")]
    public void Deserialize_InvalidJson_ThrowsJsonException(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.Throws<JsonException>(
            () => OciJsonSerializer.Deserialize<Manifest>(bytes));
    }

    [Fact]
    public void Deserialize_EmptyString_ThrowsJsonException()
    {
        var bytes = Encoding.UTF8.GetBytes("");
        Assert.Throws<JsonException>(
            () => OciJsonSerializer.Deserialize<Manifest>(bytes));
    }

    [Fact]
    public void Deserialize_NullByteArray_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(
            () => OciJsonSerializer.Deserialize<Manifest>(
                (byte[])null!));
    }

    [Fact]
    public void Deserialize_ManifestMissingConfig_ThrowsJsonException()
    {
        // Config is `required` in the C# model — .NET 8 STJ
        // enforces this and throws JsonException.
        var json = """{"schemaVersion":2,"layers":[]}""";
        var bytes = Encoding.UTF8.GetBytes(json);

        Assert.Throws<JsonException>(
            () => OciJsonSerializer.Deserialize<Manifest>(
                bytes));
    }

    [Fact]
    public void Deserialize_UnknownFieldInConfig_Succeeds()
    {
        var json = """
            {
                "schemaVersion": 2,
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "config": {
                    "mediaType": "application/vnd.oci.image.config.v1+json",
                    "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c9299874ad691",
                    "size": 2,
                    "unknownNested": "should be ignored"
                },
                "layers": []
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var manifest = OciJsonSerializer.Deserialize<Manifest>(bytes);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest!.Config.Size);
    }

    [Fact]
    public void Deserialize_UnknownFieldInPlatform_Succeeds()
    {
        var json = """
            {
                "schemaVersion": 2,
                "mediaType": "application/vnd.oci.image.index.v1+json",
                "manifests": [
                    {
                        "mediaType": "application/vnd.oci.image.manifest.v1+json",
                        "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c9299874ad691",
                        "size": 100,
                        "platform": {
                            "architecture": "amd64",
                            "os": "linux",
                            "futureField": ["a", "b"]
                        }
                    }
                ]
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var index = OciJsonSerializer.Deserialize<OciIndex>(bytes);

        Assert.NotNull(index);
        Assert.Equal("amd64", index!.Manifests[0].Platform!.Architecture);
    }

    [Fact]
    public void Deserialize_UnknownFieldInDescriptor_Succeeds()
    {
        var json = """
            {
                "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c9299874ad691",
                "size": 1024,
                "customExtension": { "nested": true }
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var descriptor = OciJsonSerializer.Deserialize<Descriptor>(bytes);

        Assert.NotNull(descriptor);
        Assert.Equal(1024, descriptor!.Size);
    }
}
