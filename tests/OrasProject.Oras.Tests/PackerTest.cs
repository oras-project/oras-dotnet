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

using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Exceptions;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests;

public class PackerTest
{
    [Fact]
    public async Task TestPackManifestImageV1_0()
    {
        var memoryTarget = new MemoryStore();

        // Test PackManifest
        var cancellationToken = new CancellationToken();
        var manifestVersion = Packer.ManifestVersion.Version1_0;
        var artifactType = "application/vnd.test";
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, manifestVersion, artifactType, new PackManifestOptions(), cancellationToken);
        Assert.NotNull(manifestDesc);

        Manifest? manifest;
        var rc = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Assert.NotNull(rc);
        using (rc)
        {
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc!);
        }
        Assert.NotNull(manifest);

        // Verify media type
        var got = manifest?.MediaType;
        Assert.Equal("application/vnd.oci.image.manifest.v1+json", got);

        // Verify config
        var expectedConfigData = System.Text.Encoding.UTF8.GetBytes("{}");
        var expectedConfig = new Descriptor
        {
            MediaType = artifactType,
            Digest = Digest.ComputeSHA256(expectedConfigData),
            Size = expectedConfigData.Length
        };
        var expectedConfigBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedConfig));
        var incomingConfig = manifest?.Config;
        var incomingConfigBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(incomingConfig));
        //Assert.True(manifest.Config.Equals(expectedConfig), $"got config = {manifest.Config}, want {expectedConfig}");
        Assert.Equal(incomingConfigBytes, expectedConfigBytes);

        // Verify layers
        var expectedLayers = new List<Descriptor>();
        Assert.True(manifest!.Layers.SequenceEqual(expectedLayers), $"got layers = {manifest.Layers}, want {expectedLayers}");

        // Verify created time annotation
        Assert.True(manifest.Annotations!.TryGetValue("org.opencontainers.image.created", out var createdTime), $"Annotation \"org.opencontainers.image.created\" not found");
        Assert.True(DateTime.TryParse(createdTime, out _), $"Error parsing created time: {createdTime}");

        // Verify descriptor annotations
        Assert.True(manifestDesc.Annotations!.SequenceEqual(manifest?.Annotations!), $"got descriptor annotations = {manifestDesc.Annotations}, want {manifest!.Annotations}");
    }

    [Fact]
    public async Task TestPackManifestImageV1_0WithoutPassingOptions()
    {
        var memoryTarget = new MemoryStore();

        // Test PackManifest
        var manifestVersion = Packer.ManifestVersion.Version1_0;
        var artifactType = "application/vnd.test";
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, manifestVersion, artifactType);
        Assert.NotNull(manifestDesc);

        Manifest? manifest;
        var rc = await memoryTarget.FetchAsync(manifestDesc);
        Assert.NotNull(rc);
        using (rc)
        {
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc!);
        }
        Assert.NotNull(manifest);

        // Verify media type
        var got = manifest?.MediaType;
        Assert.Equal("application/vnd.oci.image.manifest.v1+json", got);

        // Verify config
        var expectedConfigData = System.Text.Encoding.UTF8.GetBytes("{}");
        var expectedConfig = Descriptor.Create(expectedConfigData, artifactType);
        var expectedConfigBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedConfig));
        var incomingConfig = manifest?.Config;
        var incomingConfigBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(incomingConfig));
        //Assert.True(manifest.Config.Equals(expectedConfig), $"got config = {manifest.Config}, want {expectedConfig}");
        Assert.Equal(incomingConfigBytes, expectedConfigBytes);

        // Verify layers
        var expectedLayers = new List<Descriptor>();
        Assert.True(manifest!.Layers.SequenceEqual(expectedLayers), $"got layers = {manifest.Layers}, want {expectedLayers}");

        // Verify created time annotation
        Assert.True(manifest.Annotations!.TryGetValue("org.opencontainers.image.created", out var createdTime), $"Annotation \"org.opencontainers.image.created\" not found");
        Assert.True(DateTime.TryParse(createdTime, out _), $"Error parsing created time: {createdTime}");

        // Verify descriptor annotations
        Assert.True(manifestDesc.Annotations!.SequenceEqual(manifest?.Annotations!), $"got descriptor annotations = {manifestDesc.Annotations}, want {manifest!.Annotations}");
    }

    [Fact]
    public async Task TestPackManifestImageV1_0_WithOptions()
    {
        var memoryTarget = new MemoryStore();

        // Prepare test content
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var appendBlob = (string mediaType, byte[] blob) =>
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSHA256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        };
        var generateManifest = (Descriptor config, List<Descriptor> layers) =>
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            appendBlob(Oci.MediaType.ImageManifest, manifestBytes);
        };
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        appendBlob(Oci.MediaType.ImageConfig, getBytes("config")); // blob 0
        appendBlob(Oci.MediaType.ImageLayer, getBytes("hello world")); // blob 1
        appendBlob(Oci.MediaType.ImageLayer, getBytes("goodbye world")); // blob 2
        var layers = descs.GetRange(1, 2);
        var configBytes = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
                            {
                                MediaType = "application/vnd.test.config",
                                Digest = Digest.ComputeSHA256(configBytes),
                                Size = configBytes.Length
                            };
        var configAnnotations = new Dictionary<string, string> { { "foo", "bar" } };
        var annotations = new Dictionary<string, string>
        {
            { "org.opencontainers.image.created", "2000-01-01T00:00:00Z" },
            { "foo", "bar" }
        };
        var artifactType = "application/vnd.test";

        // Test PackManifest with ConfigDescriptor
        var opts = new PackManifestOptions
        {
            Config = configDesc,
            Layers = layers,
            ManifestAnnotations = annotations,
            ConfigAnnotations = configAnnotations
        };
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, artifactType, opts, cancellationToken);

        var expectedManifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Config = configDesc,
            Layers = layers,
            Annotations = annotations
        };
        var expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest);

        using var rc = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Assert.NotNull(rc);
        var memoryStream = new MemoryStream();
        await rc.CopyToAsync(memoryStream);
        var got = memoryStream.ToArray();
        Assert.Equal(expectedManifestBytes, got);

        // Verify descriptor
        var expectedManifestDesc = new Descriptor
                                        {
                                            MediaType = expectedManifest.MediaType,
                                            Digest = Digest.ComputeSHA256(expectedManifestBytes),
                                            Size = expectedManifestBytes.Length
        };
        expectedManifestDesc.ArtifactType = expectedManifest.Config.MediaType;
        expectedManifestDesc.Annotations = expectedManifest.Annotations;
        var expectedManifestDescBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedManifestDesc));
        var manifestDescBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifestDesc));
        Assert.Equal(expectedManifestDescBytes, manifestDescBytes);

        // Test PackManifest without ConfigDescriptor
        opts = new PackManifestOptions
        {
            Layers = layers,
            ManifestAnnotations = annotations,
            ConfigAnnotations = configAnnotations
        };
        manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, artifactType, opts, cancellationToken);

        var expectedConfigDesc = new Descriptor
                                    {
                                        MediaType = artifactType,
                                        Digest = Digest.ComputeSHA256(configBytes),
                                        Annotations = configAnnotations,
                                        Size = configBytes.Length
                                    };
        expectedManifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Config = expectedConfigDesc,
            Layers = layers,
            Annotations = annotations
        };
        expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest);

        using var rc2 = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Assert.NotNull(rc2);
        Manifest? manifest2 = await JsonSerializer.DeserializeAsync<Manifest>(rc2!);
        var got2 = JsonSerializer.SerializeToUtf8Bytes(manifest2);
        Assert.Equal(expectedManifestBytes, got2);

        // Verify descriptor
        expectedManifestDesc = new Descriptor
                                {
                                    MediaType = expectedManifest.MediaType,
                                    Digest = Digest.ComputeSHA256(expectedManifestBytes),
                                    Size = expectedManifestBytes.Length
                                };
        expectedManifestDesc.ArtifactType = expectedManifest.Config.MediaType;
        expectedManifestDesc.Annotations = expectedManifest.Annotations;
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expectedManifestDesc), JsonSerializer.SerializeToUtf8Bytes(manifestDesc));
    }

    [Fact]
    public async Task TestPackManifestImageV1_0_SubjectUnsupported()
    {
        var memoryTarget = new MemoryStore();

        // Prepare test content
        var artifactType = "application/vnd.test";
        var subjectManifest = Encoding.UTF8.GetBytes(@"{""layers"":[]}");
        var subjectDesc = new Descriptor
        {
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Digest = Digest.ComputeSHA256(subjectManifest),
            Size = subjectManifest.Length
        };

        // Test PackManifest with ConfigDescriptor
        var cancellationToken = new CancellationToken();
        var opts = new PackManifestOptions
        {
            Subject = subjectDesc
        };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, artifactType, opts, cancellationToken);
        });

        Assert.Equal("Subject is not supported for manifest version 1.0.", exception.Message);
    }

    [Fact]
    public async Task TestPackManifestImageV1_0_NoArtifactType()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Call PackManifest with empty artifact type
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, "", new PackManifestOptions(), cancellationToken);

        var rc = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Assert.NotNull(rc);
        Manifest? manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc);

        // Verify artifact type and config media type
        
        Assert.Equal(Packer.UnknownConfig, manifestDesc.ArtifactType);
        Assert.Equal(Packer.UnknownConfig, manifest!.Config.MediaType);
    }

    [Fact]
    public void TestPackManifestImageV1_0_InvalidMediaType()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Test invalid artifact type + valid config media type
        string artifactType = "random";
        byte[] configBytes = System.Text.Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
                            {
                                MediaType = "application/vnd.test.config",
                                Digest = Digest.ComputeSHA256(configBytes),
                                Size = configBytes.Length
                            };
        var opts = new PackManifestOptions
        {
            Config = configDesc
        };

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, artifactType, opts, cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.Null(ex); // Expecting no exception
        }

        // Test invalid config media type + valid artifact type
        artifactType = "application/vnd.test";
        configDesc = new Descriptor
                            {
                                MediaType = "random",
                                Digest = Digest.ComputeSHA256(configBytes),
                                Size = configBytes.Length
                            };
        opts = new PackManifestOptions
        {
            Config = configDesc
        };

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, artifactType, opts, cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.True(ex is InvalidMediaTypeException, $"Expected InvalidMediaTypeException but got {ex.GetType().Name}");
        }
    }

    [Fact]
    public void TestPackManifestImageV1_0_InvalidDateTimeFormat()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        var opts = new PackManifestOptions
        {
            ManifestAnnotations = new Dictionary<string, string>
            {
                { "org.opencontainers.image.created", "2000/01/01 00:00:00" }
            }
        };

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_0, "", opts, cancellationToken);
        }
        catch (Exception ex)
        {
            // Check if the caught exception is of type InvalidDateTimeFormatException
            Assert.True(ex is InvalidDateTimeFormatException, $"Expected InvalidDateTimeFormatException but got {ex.GetType().Name}");
        }
    }

    [Fact]
    public async Task TestPackManifestImageV1_1()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Test PackManifest
        var artifactType = "application/vnd.test";
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, new PackManifestOptions(), cancellationToken);
        
        // Fetch and decode the manifest
        var rc = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Manifest? manifest;
        Assert.NotNull(rc);
        using (rc)
        {
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc);
        }

        // Verify layers
        var emptyConfigBytes = Encoding.UTF8.GetBytes("{}");
        var emptyJSON = Descriptor.Empty;
        var expectedLayers = new List<Descriptor> { emptyJSON };
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expectedLayers), JsonSerializer.SerializeToUtf8Bytes(manifest!.Layers));
    }

    [Fact]
    public async Task TestPackManifestImageV1_1WithoutPassingOptions()
    {
        var memoryTarget = new MemoryStore();
        
        // Test PackManifest
        var artifactType = "application/vnd.test";
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType);

        // Fetch and decode the manifest
        var rc = await memoryTarget.FetchAsync(manifestDesc);
        Manifest? manifest;
        Assert.NotNull(rc);
        using (rc)
        {
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc);
        }

        // Verify layers
        var emptyConfigBytes = Encoding.UTF8.GetBytes("{}");
        var emptyJSON = new Descriptor
        {
            MediaType = "application/vnd.oci.empty.v1+json",
            Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
            Size = emptyConfigBytes.Length,
            Data = emptyConfigBytes
        };
        var expectedLayers = new List<Descriptor> { emptyJSON };
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expectedLayers), JsonSerializer.SerializeToUtf8Bytes(manifest!.Layers));
    }

    [Fact]
    public async Task TestPackManifestImageV1_1_WithOptions()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Prepare test content
        byte[] hellogBytes = System.Text.Encoding.UTF8.GetBytes("hello world");
        byte[] goodbyeBytes = System.Text.Encoding.UTF8.GetBytes("goodbye world");
        var layers = new List<Descriptor>
        {
            new Descriptor
            {
                MediaType = "test",
                Data = hellogBytes,
                Digest = Digest.ComputeSHA256(hellogBytes),
                Size = hellogBytes.Length
            },
            new Descriptor
            {
                MediaType = "test",
                Data = goodbyeBytes,
                Digest = Digest.ComputeSHA256(goodbyeBytes),
                Size = goodbyeBytes.Length
            }
        };
        var configBytes = System.Text.Encoding.UTF8.GetBytes("config");
        var configDesc = new Descriptor
        {
            MediaType = "application/vnd.test",
            Data = configBytes,
            Digest = Digest.ComputeSHA256(configBytes),
            Size = configBytes.Length
        };
        var configAnnotations = new Dictionary<string, string> { { "foo", "bar" } };
        var annotations = new Dictionary<string, string>
        {
            { "org.opencontainers.image.created", "2000-01-01T00:00:00Z" },
            { "foo", "bar" }
        };
        var artifactType = "application/vnd.test";
        var subjectManifest = System.Text.Encoding.UTF8.GetBytes("{\"layers\":[]}");
        var subjectDesc = new Descriptor
        {
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Digest = Digest.ComputeSHA256(subjectManifest),
            Size = subjectManifest.Length
        };

        // Test PackManifest with ConfigDescriptor
        var opts = new PackManifestOptions
        {
            Subject = subjectDesc,
            Layers = layers,
            Config = configDesc,
            ConfigAnnotations = configAnnotations,
            ManifestAnnotations = annotations
        };
        var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, opts, cancellationToken);
        
        var expectedManifest = new Manifest
        {
            SchemaVersion = 2, // Historical value, doesn't pertain to OCI or Docker version
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            ArtifactType = artifactType,
            Subject = subjectDesc,
            Config = configDesc,
            Layers = layers,
            Annotations = annotations
        };
        var expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest);
        using var rc = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Manifest? manifest = await JsonSerializer.DeserializeAsync<Manifest>(rc);
        var got = JsonSerializer.SerializeToUtf8Bytes(manifest);
        Assert.Equal(expectedManifestBytes, got);

        // Verify descriptor
        var expectedManifestDesc = new Descriptor
                                        {
                                            MediaType = expectedManifest.MediaType,
                                            Digest = Digest.ComputeSHA256(expectedManifestBytes),
                                            Size = expectedManifestBytes.Length
        };
        expectedManifestDesc.ArtifactType = expectedManifest.Config.MediaType;
        expectedManifestDesc.Annotations = expectedManifest.Annotations;
        var expectedManifestDescBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedManifestDesc));
        var manifestDescBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifestDesc));
        Assert.Equal(expectedManifestDescBytes, manifestDescBytes);
        
        // Test PackManifest with ConfigDescriptor, but without artifactType
        opts = new PackManifestOptions
        {
            Subject = subjectDesc,
            Layers = layers,
            Config = configDesc,
            ConfigAnnotations = configAnnotations,
            ManifestAnnotations = annotations
        };

        manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, null, opts, cancellationToken);
        expectedManifest.ArtifactType = null;
        expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest);
        using var rc2 = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Manifest? manifest2 = await JsonSerializer.DeserializeAsync<Manifest>(rc2);
        var got2 = JsonSerializer.SerializeToUtf8Bytes(manifest2);
        Assert.Equal(expectedManifestBytes, got2);

        expectedManifestDesc = new Descriptor
                                {
                                    MediaType = expectedManifest.MediaType,
                                    Digest = Digest.ComputeSHA256(expectedManifestBytes),
                                    Size = expectedManifestBytes.Length
                                };
        expectedManifestDesc.Annotations = expectedManifest.Annotations;
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expectedManifestDesc), JsonSerializer.SerializeToUtf8Bytes(manifestDesc));

        // Test Pack without ConfigDescriptor
        opts = new PackManifestOptions
        {
            Subject = subjectDesc,
            Layers = layers,
            ConfigAnnotations = configAnnotations,
            ManifestAnnotations = annotations
        };

        manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, opts, cancellationToken);
        var emptyConfigBytes = Encoding.UTF8.GetBytes("{}");
        var emptyJSON = new Descriptor
                            {
                                MediaType = "application/vnd.oci.empty.v1+json",
                                Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
                                Size = emptyConfigBytes.Length,
                                Data = emptyConfigBytes
                            };
        var expectedConfigDesc = emptyJSON;
        expectedManifest.ArtifactType = artifactType;
        expectedManifest.Config = expectedConfigDesc;
        expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest);
        using var rc3 = await memoryTarget.FetchAsync(manifestDesc, cancellationToken);
        Manifest? manifest3 = await JsonSerializer.DeserializeAsync<Manifest>(rc3);
        var got3 = JsonSerializer.SerializeToUtf8Bytes(manifest3);
        Assert.Equal(expectedManifestBytes, got3);

        expectedManifestDesc = new Descriptor
                                {
                                    MediaType = expectedManifest.MediaType,
                                    Digest = Digest.ComputeSHA256(expectedManifestBytes),
                                    Size = expectedManifestBytes.Length
                                };
        expectedManifestDesc.ArtifactType = artifactType;
        expectedManifestDesc.Annotations = expectedManifest.Annotations;
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expectedManifestDesc), JsonSerializer.SerializeToUtf8Bytes(manifestDesc));
    }

    [Fact]
    public async Task TestPackManifestImageV1_1_NoArtifactType()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Test no artifact type and no config
        try
        {
            var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, "", new PackManifestOptions(), cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.True(ex is MissingArtifactTypeException, $"Expected Artifact found in manifest without config");
        }
        // Test no artifact type and config with empty media type
        var emptyConfigBytes = Encoding.UTF8.GetBytes("{}");
        var opts = new PackManifestOptions
        {
            Config = new Descriptor
            {
                MediaType = "application/vnd.oci.empty.v1+json",
                Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
                Size = emptyConfigBytes.Length,
                Data = emptyConfigBytes
            }
        };
        try
        {
            var manifestDesc = await Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, "", opts, cancellationToken);
        }
        catch (Exception ex)
        {
            // Check if the caught exception is of type InvalidDateTimeFormatException
            Assert.True(ex is MissingArtifactTypeException, $"Expected Artifact found in manifest with empty config");
        }
    }

    [Fact]
    public void Test_PackManifestImageV1_1_InvalidMediaType()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        // Test invalid artifact type + valid config media type
        var artifactType = "random";
        byte[] configBytes = System.Text.Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
                            {
                                MediaType = "application/vnd.test.config",
                                Digest = Digest.ComputeSHA256(configBytes),
                                Size = configBytes.Length
                            };
        var opts = new PackManifestOptions
        {
            Config = configDesc
        };

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, opts, cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.Null(ex); // Expecting no exception
        }

        // Test invalid config media type + valid artifact type
        artifactType = "application/vnd.test";
        configDesc = new Descriptor
                            {
                                MediaType = "random",
                                Digest = Digest.ComputeSHA256(configBytes),
                                Size = configBytes.Length
                            };
        opts = new PackManifestOptions
        {
            Config = configDesc
        };

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, opts, cancellationToken);
        }
        catch (Exception ex)
        {
            Assert.True(ex is InvalidMediaTypeException, $"Expected InvalidMediaTypeException but got {ex.GetType().Name}");
        }    
    }

    [Fact]
    public void TestPackManifestImageV1_1_InvalidDateTimeFormat()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        var opts = new PackManifestOptions
        {
            ManifestAnnotations = new Dictionary<string, string>
            {
                { "org.opencontainers.image.created", "2000/01/01 00:00:00" }
            }
        };

        var artifactType = "application/vnd.test";
        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, Packer.ManifestVersion.Version1_1, artifactType, opts, cancellationToken);
        }
        catch (Exception ex)
        {
            // Check if the caught exception is of type InvalidDateTimeFormatException
            Assert.True(ex is InvalidDateTimeFormatException, $"Expected InvalidDateTimeFormatException but got {ex.GetType().Name}");
        }

    }

    [Fact]
    public void TestPackManifestUnsupportedPackManifestVersion()
    {
        var memoryTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();

        try
        {
            var manifestDesc = Packer.PackManifestAsync(memoryTarget, (Packer.ManifestVersion)(-1), "", new PackManifestOptions(), cancellationToken);
        }
        catch (Exception ex)
        {
            // Check if the caught exception is of type InvalidDateTimeFormatException
            Assert.True(ex is NotSupportedException, $"Expected InvalidDateTimeFormatException but got {ex.GetType().Name}");
        }
    }
}
