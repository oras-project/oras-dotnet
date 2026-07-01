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
using OrasProject.Oras.Exceptions;
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
                    "digest": "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
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
                        "digest": "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
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
                "digest": "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
                "size": 1024,
                "customExtension": { "nested": true }
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var descriptor = OciJsonSerializer.Deserialize<Descriptor>(bytes);

        Assert.NotNull(descriptor);
        Assert.Equal(1024, descriptor!.Size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData(":44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a")]
    public void Descriptor_Validate_InvalidDigest_ThrowsInvalidDescriptorException(string invalidDigest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = invalidDigest
        };

        Assert.Throws<InvalidDescriptorException>(() => descriptor.Validate());
    }

    // Per OCI image-spec v1.1.1: sha256 encoded MUST match /[a-f0-9]{64}/
    [Theory]
    [InlineData("sha256:tooshort")]
    [InlineData("sha256:44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A")] // uppercase not allowed
    [InlineData("sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8")] // 63 chars
    [InlineData("sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8aa")] // 65 chars
    [InlineData("sha512:tooshort")]
    [InlineData("sha512:cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3")] // 127 chars
    public void Descriptor_Validate_InvalidRegisteredDigest_ThrowsInvalidDescriptorException(string invalidDigest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = invalidDigest
        };

        Assert.Throws<InvalidDescriptorException>(() => descriptor.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Descriptor_TryValidate_EmptyMediaType_ReturnsFalse(string mediaType)
    {
        var descriptor = new Descriptor
        {
            MediaType = mediaType,
            Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"
        };

        var result = descriptor.TryValidate(out var error);

        Assert.False(result);
        Assert.Equal(Descriptor.ErrMediaTypeEmpty, error);
    }

    [Fact]
    public void Descriptor_TryValidate_NegativeSize_ReturnsFalse()
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
            Size = -1
        };

        var result = descriptor.TryValidate(out var error);

        Assert.False(result);
        Assert.Equal(Descriptor.ErrSizeNegative, error);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData(":44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a")]
    public void Descriptor_TryValidate_InvalidDigestFormat_ErrorContainsDigest(string invalidDigest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = invalidDigest
        };

        var result = descriptor.TryValidate(out var error);

        Assert.False(result);
        Assert.Contains(invalidDigest, error);
    }

    [Theory]
    [InlineData("sha256:tooshort")]
    [InlineData("sha256:44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A")]
    [InlineData("sha512:tooshort")]
    public void Descriptor_TryValidate_InvalidRegisteredDigest_ErrorContainsDigest(string invalidDigest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = invalidDigest
        };

        var result = descriptor.TryValidate(out var error);

        Assert.False(result);
        Assert.Contains(invalidDigest, error);
    }

    [Fact]
    public void Descriptor_TryValidate_EmptyDigest_ReturnsFalse()
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = ""
        };

        var result = descriptor.TryValidate(out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // Per OCI spec: unrecognized algorithms SHOULD pass if they match the general grammar
    [Theory]
    [InlineData("multihash+base58:QmRZxt2b1FVZPNqd8hsiykDL3TdBDeTSPX9Kv46HmX4Gx8")]
    [InlineData("sha256+b64u:LCa0a2j_xo_5m0U8HTBBNBNCLXBkg7-g-YpeiGJm564")]
    public void Descriptor_TryValidate_UnrecognizedAlgorithm_ReturnsTrue(string digest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = digest
        };

        var result = descriptor.TryValidate(out var error);

        Assert.True(result);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a")]
    [InlineData("sha512:cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
    public void Descriptor_TryValidate_ValidDigest_ReturnsTrue(string validDigest)
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = validDigest
        };

        var result = descriptor.TryValidate(out var error);

        Assert.True(result);
        Assert.Empty(error);
    }

    [Fact]
    public void Descriptor_Validate_ValidDescriptor_DoesNotThrow()
    {
        var descriptor = new Descriptor
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
            Size = 1024
        };

        descriptor.Validate(); // should not throw
    }
}
