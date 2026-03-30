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
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;
using OciIndex = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Serialization;

public partial class ManifestSerializationTest
{
    #region Docker JSON Constants

    private const string DockerManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
            "config": {
                "mediaType": "application/vnd.docker.container.image.v1+json",
                "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                "size": 1024
            },
            "layers": [
                {
                    "mediaType": "application/vnd.docker.image.rootfs.diff.tar.gzip",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444",
                    "size": 2048
                }
            ]
        }
        """;

    private const string DockerManifestListJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.docker.distribution.manifest.list.v2+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
                    "digest": "sha256:e692418e4cbaf90ca69d05a66403747baa33ee08806650b51fab815ad7fc331f",
                    "size": 7143,
                    "platform": {
                        "architecture": "amd64",
                        "os": "linux"
                    }
                },
                {
                    "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
                    "digest": "sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270",
                    "size": 7682,
                    "platform": {
                        "architecture": "arm64",
                        "os": "linux",
                        "variant": "v8"
                    }
                }
            ]
        }
        """;

    #endregion

    #region Docker Manifest Tests

    [Fact]
    public void Deserialize_DockerManifest_PreservesFields()
    {
        var bytes =
            Encoding.UTF8.GetBytes(DockerManifestJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.Equal(
            Docker.MediaType.Manifest, m.MediaType);
        Assert.Equal(
            Docker.MediaType.Config,
            m.Config.MediaType);
        Assert.Single(m.Layers);
    }

    [Fact]
    public void Deserialize_DockerManifestList_FieldAssertions()
    {
        var bytes =
            Encoding.UTF8.GetBytes(DockerManifestListJson);
        var idx =
            OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Equal(2, idx.Manifests.Count);

        var first = idx.Manifests[0];
        Assert.NotNull(first.Platform);
        Assert.Equal("amd64", first.Platform!.Architecture);
        Assert.Equal("linux", first.Platform.Os);

        var second = idx.Manifests[1];
        Assert.NotNull(second.Platform);
        Assert.Equal("arm64", second.Platform!.Architecture);
        Assert.Equal("v8", second.Platform!.Variant);
    }

    [Fact]
    public void Serialize_DockerManifest_MediaTypePresent()
    {
        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = Docker.MediaType.Manifest,
            Config = Descriptor.Empty,
            Layers = new List<Descriptor>()
        };

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(manifest));

        Assert.Contains("\"mediaType\"", json);
        Assert.Contains(
            Docker.MediaType.Manifest, json);
    }

    #endregion
}
