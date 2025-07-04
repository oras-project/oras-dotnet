﻿// Copyright The ORAS Authors.
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

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using OrasProject.Oras.Content;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Content.Digest;
using Xunit;
using OrasProject.Oras.Exceptions;

namespace OrasProject.Oras.Tests.Content;

public class ExtensionsTest
{

    private const string _dockerContentDigestHeader = "Docker-Content-Digest";

    [Fact]
    public async Task GetSuccessorsAsync_ImageManifestWithSubject_ReturnsSubjectConfigAndLayers()
    {
        // Oci Image Manifest
        var (manifest, _) = RandomManifest();
        manifest.Subject = RandomDescriptor();
        manifest.Layers.Add(RandomDescriptor());

        var expectedManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req,
            CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}")
            {
                if (!req.Headers.Accept.Contains(new MediaTypeWithQualityHeaderValue(MediaType.ImageManifest)))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content = new ByteArrayContent(expectedManifestBytes);
                res.Content.Headers.Add("Content-Type", expectedManifestDesc.MediaType);
                res.Headers.Add(_dockerContentDigestHeader, expectedManifestDesc.Digest);

                res.StatusCode = HttpStatusCode.OK;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHttpRequestHandler),
            PlainHttp = true,
        });

        // act
        var cancellationToken = new CancellationToken();
        var actualManifestSuccessors = (await repo.GetSuccessorsAsync(expectedManifestDesc, cancellationToken)).ToList();


        // assert
        Assert.Equal(3, actualManifestSuccessors.Count);
        Assert.Equal(manifest.Subject.Digest, actualManifestSuccessors[0].Digest);
        Assert.Equal(manifest.Config.Digest, actualManifestSuccessors[1].Digest);
        Assert.Equal(manifest.Layers[0].Digest, actualManifestSuccessors[2].Digest);
    }

    [Fact]
    public async Task GetSuccessorsAsync_IndexManifestWithSubject_ReturnsSubjectConfigAndLayers()
    {
        // Oci Index Manifest
        var expectedIndexManifest = RandomIndex();
        expectedIndexManifest.Subject = RandomDescriptor();
        var expectedIndexManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndexManifest));
        var expectedIndexManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSha256(expectedIndexManifestBytes),
            Size = expectedIndexManifestBytes.Length,
            ArtifactType = MediaType.ImageIndex,
        };


        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req,
            CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };

            if (req.Method == HttpMethod.Get &&
                 req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}")
            {
                if (!req.Headers.Accept.Contains(new MediaTypeWithQualityHeaderValue(MediaType.ImageIndex)))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content = new ByteArrayContent(expectedIndexManifestBytes);
                res.Content.Headers.Add("Content-Type", expectedIndexManifest.MediaType);
                res.Headers.Add(_dockerContentDigestHeader, expectedIndexManifestDesc.Digest);

                res.StatusCode = HttpStatusCode.OK;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHttpRequestHandler),
            PlainHttp = true,
        });

        // act
        var cancellationToken = new CancellationToken();
        var actualIndexManifestSuccessors = (await repo.GetSuccessorsAsync(expectedIndexManifestDesc, cancellationToken)).ToList();


        // assert
        Assert.Equal(4, actualIndexManifestSuccessors.Count);
        Assert.Equal(expectedIndexManifest.Subject.Digest, actualIndexManifestSuccessors[0].Digest);
        Assert.Equal(expectedIndexManifest.Manifests[0].Digest, actualIndexManifestSuccessors[1].Digest);
        Assert.Equal(expectedIndexManifest.Manifests[1].Digest, actualIndexManifestSuccessors[2].Digest);
        Assert.Equal(expectedIndexManifest.Manifests[2].Digest, actualIndexManifestSuccessors[3].Digest);
    }


    [Fact]
    public async Task GetSuccessorsAsync_ImageConfig_ReturnsEmptyList()
    {
        var imageConfig = "hello world"u8.ToArray();
        var imageConfigDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = ComputeSha256(imageConfig),
            Size = imageConfig.Length
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient((_, _) => new HttpResponseMessage()),
            PlainHttp = true,
        });

        // act
        var cancellationToken = new CancellationToken();
        var actualImageConfig = (await repo.GetSuccessorsAsync(imageConfigDesc, cancellationToken)).ToList();

        // assert
        Assert.Empty(actualImageConfig);
    }

    [Fact]
    public async Task ReadStreamWithLimitAsync_StreamWithinLimit_DoesNotThrow()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(data);
        long limit = data.Length + 10;

        // act
        var result = await stream.ReadStreamWithLimitAsync(limit, CancellationToken.None);

        // assert
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task ReadStreamWithLimitAsync_StreamExceedsLimit_Throws()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(data);
        long limit = data.Length - 1;

        // act & assert
        await Assert.ThrowsAsync<SizeLimitExceededException>(async () =>
        {
            await stream.ReadStreamWithLimitAsync(limit, CancellationToken.None);
        });
    }

    [Fact]
    public async Task ReadAllAsync_ValidStream_ReturnsCorrectBytes()
    {
        // Arrange
        byte[] expectedBytes = Encoding.UTF8.GetBytes("test");
        string expectedDigest = Digest.ComputeSha256(expectedBytes);
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Size = expectedBytes.Length,
            Digest = expectedDigest
        };

        using var stream = new MemoryStream(expectedBytes);

        // Act
        var result = await stream.ReadAllAsync(descriptor, CancellationToken.None);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task ReadAllAsync_StreamShorterThanDescriptor_ThrowsArgumentException()
    {
        // Arrange
        byte[] bytes = Encoding.UTF8.GetBytes("test");
        string digest = Digest.ComputeSha256(bytes);
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            // set descriptor size bigger than actual stream length
            Size = bytes.Length + 1,
            Digest = digest
        };

        using var stream = new MemoryStream(bytes);

        // Act & Assert
        MismatchedSizeException ex = await Assert.ThrowsAsync<MismatchedSizeException>(() => stream.ReadAllAsync(descriptor, CancellationToken.None));
        Assert.Contains("is larger than the content length", ex.Message);
    }

    [Fact]
    public async Task ReadAllAsync_StreamLongerThanDescriptor_ThrowsArgumentException()
    {
        // Arrange
        byte[] content = Encoding.UTF8.GetBytes("test");
        // Use only the first part as valid content.
        int validSize = content.Length - 1;
        byte[] validBytes = new byte[validSize];
        Array.Copy(content, validBytes, validSize);
        string validDigest = Digest.ComputeSha256(validBytes);
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Size = validSize,
            Digest = validDigest
        };

        using var stream = new MemoryStream(content);

        // Act & Assert
        MismatchedSizeException ex = await Assert.ThrowsAsync<MismatchedSizeException>(() => stream.ReadAllAsync(descriptor, CancellationToken.None));
        Assert.Contains("is smaller than the content length", ex.Message);
    }

    [Fact]
    public async Task ReadAllAsync_InvalidDigest_ThrowsMismatchedDigestException()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        // Provide a wrong digest deliberately.
        string wrongDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Size = data.Length,
            Digest = wrongDigest
        };

        using var stream = new MemoryStream(data);

        // Act & Assert
        await Assert.ThrowsAsync<MismatchedDigestException>(() => stream.ReadAllAsync(descriptor, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAllAsync_NegativeDescriptorSize_ThrowsInvalidDescriptorSizeException()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Any content");
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Size = -1,
            Digest = Digest.ComputeSha256(data)
        };

        using var stream = new MemoryStream(data);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDescriptorSizeException>(() => stream.ReadAllAsync(descriptor, CancellationToken.None));
    }
}
