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
using System.Text;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;
using OciIndex = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Serialization;

public partial class ManifestSerializationTest
{
    private const string MinimalIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ccc333ccc3",
                    "size": 500
                },
                {
                    "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444ddd444ddd4",
                    "size": 600
                }
            ]
        }
        """;

    private const string FullIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ccc333ccc3",
                    "size": 500,
                    "platform": {
                        "architecture": "amd64",
                        "os": "linux"
                    }
                },
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444ddd444ddd4",
                    "size": 600,
                    "platform": {
                        "architecture": "arm64",
                        "os": "linux",
                        "variant": "v8"
                    }
                }
            ],
            "annotations": {
                "org.opencontainers.image.ref.name": "latest",
                "org.example+index/key+1": "index+value"
            }
        }
        """;

    private const string SpecMultiPlatformIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:e692418e4cbaf90ca69d05a66403747baa33ee08806650b51fab815ad7fc331f",
                    "size": 7143,
                    "platform": {
                        "architecture": "amd64",
                        "os": "linux"
                    }
                },
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270",
                    "size": 7682,
                    "platform": {
                        "architecture": "arm64",
                        "os": "linux"
                    }
                }
            ]
        }
        """;

    private const string NestedIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ccc333ccc3",
                    "size": 500,
                    "platform": {
                        "architecture": "amd64",
                        "os": "linux"
                    }
                },
                {
                    "mediaType": "application/vnd.oci.image.index.v1+json",
                    "digest": "sha256:ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444eee555eee555eee5",
                    "size": 900
                }
            ]
        }
        """;

    private const string EmptyManifestsIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": []
        }
        """;

    private const string NullManifestsIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": null
        }
        """;

    [Theory]
    [MemberData(nameof(IndexFieldFixtures))]
    public void Deserialize_Index_PreservesAllFields(
        string json,
        int expectedManifestCount,
        bool hasAnnotations)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Equal(2, idx.SchemaVersion);
        Assert.Equal(expectedManifestCount, idx.Manifests.Count);

        if (hasAnnotations)
        {
            Assert.NotNull(idx.Annotations);
            Assert.NotEmpty(idx.Annotations!);
        }

        foreach (var desc in idx.Manifests)
        {
            Assert.False(string.IsNullOrEmpty(desc.MediaType));
            Assert.False(string.IsNullOrEmpty(desc.Digest));
            Assert.True(desc.Size > 0);
        }
    }

    [Fact]
    public void GenerateIndex_ProducesValidDescriptorAndContent()
    {
        var manifests = new List<Descriptor>
        {
            new()
            {
                Digest = "sha256:aaa111aaa111aaa111aaa111aaa111aaa111aaa111aaa111aaa111aaa111aaaa",
                MediaType = MediaType.ImageManifest,
                Size = 100
            },
            new()
            {
                Digest = "sha256:bbb222bbb222bbb222bbb222bbb222bbb222bbb222bbb222bbb222bbb222bbbb",
                MediaType = MediaType.ImageManifest,
                Size = 200
            }
        };

        var (desc, content) = OciIndex.GenerateIndex(manifests);

        Assert.Equal(MediaType.ImageIndex, desc.MediaType);
        Assert.Equal(content.Length, desc.Size);
        Assert.Equal(
            Digest.ComputeSha256(content), desc.Digest);

        var idx = OciJsonSerializer.Deserialize<OciIndex>(content)!;
        Assert.Equal(2, idx.Manifests.Count);
        Assert.Equal(manifests.Count, idx.Manifests.Count);
        Assert.Equal(manifests[0].Digest, idx.Manifests[0].Digest);
        Assert.Equal(manifests[0].MediaType, idx.Manifests[0].MediaType);
        Assert.Equal(manifests[0].Size, idx.Manifests[0].Size);
        Assert.Equal(manifests[1].Digest, idx.Manifests[1].Digest);
        Assert.Equal(manifests[1].MediaType, idx.Manifests[1].MediaType);
        Assert.Equal(manifests[1].Size, idx.Manifests[1].Size);
        Assert.Equal(MediaType.ImageIndex, idx.MediaType);
        Assert.Equal(2, idx.SchemaVersion);
    }

    [Fact]
    public void GenerateIndex_EmptyManifests_ProducesValidOutput()
    {
        var manifests = new List<Descriptor>();
        var (desc, content) = OciIndex.GenerateIndex(manifests);

        Assert.Equal(MediaType.ImageIndex, desc.MediaType);
        Assert.Equal(content.Length, desc.Size);
        Assert.Equal(
            Digest.ComputeSha256(content), desc.Digest);

        var idx = OciJsonSerializer.Deserialize<OciIndex>(content)!;
        Assert.Empty(idx.Manifests);
        Assert.Equal(MediaType.ImageIndex, idx.MediaType);
        Assert.Equal(2, idx.SchemaVersion);
    }

    [Fact]
    public void Deserialize_SpecMultiPlatformIndex_PlatformAssertions()
    {
        var bytes = Encoding.UTF8.GetBytes(SpecMultiPlatformIndexJson);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Equal(2, idx.Manifests.Count);

        var amd64 = idx.Manifests[0];
        Assert.NotNull(amd64.Platform);
        Assert.Equal("amd64", amd64.Platform!.Architecture);
        Assert.Equal("linux", amd64.Platform.Os);

        var arm64 = idx.Manifests[1];
        Assert.NotNull(arm64.Platform);
        Assert.Equal("arm64", arm64.Platform!.Architecture);
        Assert.Equal("linux", arm64.Platform.Os);
    }

    [Fact]
    public void Deserialize_NestedIndex_MixedMediaTypes()
    {
        var bytes = Encoding.UTF8.GetBytes(NestedIndexJson);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Equal(2, idx.Manifests.Count);
        Assert.Equal(
            MediaType.ImageManifest,
            idx.Manifests[0].MediaType);
        Assert.Equal(
            MediaType.ImageIndex,
            idx.Manifests[1].MediaType);
    }

    [Fact]
    public void Serialize_IndexEmptyManifests_ArrayPresent()
    {
        var index = new OciIndex(new List<Descriptor>());
        var bytes = OciJsonSerializer.SerializeToUtf8Bytes(index);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"manifests\":[]", json);
    }

    [Fact]
    public void Deserialize_NullManifests_PreservesNull()
    {
        // Some registries emit a null `manifests` value. The model preserves it as-is
        // rather than normalizing to [], so an SDK author can round-trip exactly what
        // was received; consumers guard against null instead.
        var bytes = Encoding.UTF8.GetBytes(NullManifestsIndexJson);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Null(idx.Manifests);
    }

    [Fact]
    public void Serialize_NullManifests_WritesNullNotEmptyArray()
    {
        // Round-trip: a null `manifests` value must serialize back to `null`, never `[]`.
        var bytes = Encoding.UTF8.GetBytes(NullManifestsIndexJson);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        var serialized = OciJsonSerializer.SerializeToUtf8Bytes(idx);
        var json = Encoding.UTF8.GetString(serialized);

        Assert.Contains("\"manifests\":null", json);
        Assert.DoesNotContain("\"manifests\":[]", json);
    }

    public static IEnumerable<object[]> IndexFieldFixtures()
    {
        yield return new object[]
            { MinimalIndexJson, 2, false };
        yield return new object[]
            { FullIndexJson, 2, true };
        yield return new object[]
            { EmptyManifestsIndexJson, 0, false };
    }
}
