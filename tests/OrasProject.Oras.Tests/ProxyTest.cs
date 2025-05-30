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

using Moq;
using Xunit;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Registry;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;

namespace OrasProject.Oras.Tests;

public class ProxyTest
{
    [Fact]
    public async Task FetchAsync_CacheHit_ReturnsCacheStream()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };
        var cache = new MemoryStorage();
        var sourceMock = new Mock<ITarget>();
        var cancellationToken = CancellationToken.None;
        using var expectedStream = new MemoryStream(manifestBytes);

        await cache.PushAsync(manifestDesc, expectedStream, cancellationToken);
        var proxy = new Proxy { Cache = cache, Source = sourceMock.Object };

        var result = await proxy.FetchAsync(manifestDesc, cancellationToken);

        // Assert that the returned stream contains the expected content
        var actualBytes = new MemoryStream();
        await result.CopyToAsync(actualBytes, cancellationToken);
        Assert.Equal(manifestBytes, actualBytes.ToArray());
        sourceMock.Verify(s => s.FetchAsync(It.IsAny<Descriptor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchAsync_CacheMiss_PushesAndReturnsCacheStream()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };

        var cache = new MemoryStorage();
        var sourceMock = new Mock<ITarget>();
        var ct = CancellationToken.None;
        using var expectedStream = new MemoryStream(manifestBytes);

       
        sourceMock.Setup(s => s.FetchAsync(manifestDesc, ct)).ReturnsAsync(expectedStream);
       
        var proxy = new Proxy { Cache = cache, Source = sourceMock.Object };

        var result = await proxy.FetchAsync(manifestDesc, ct);
        
        var actualBytes = new MemoryStream();
        await result.CopyToAsync(actualBytes, ct);
        Assert.Equal(manifestBytes, actualBytes.ToArray());
        sourceMock.Verify(s => s.FetchAsync(manifestDesc, ct), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_CacheMiss_PushThrows_AlreadyExistsException_ReturnsCacheStream()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };
        var cacheMock = new Mock<IStorage>();
        var sourceMock = new Mock<ITarget>();
        var ct = CancellationToken.None;
        using var expectedStream = new MemoryStream(manifestBytes);

        cacheMock.Setup(cache => cache.ExistsAsync(manifestDesc, ct)).ReturnsAsync(false);
        cacheMock.Setup(cache => cache.PushAsync(manifestDesc, It.IsAny<Stream>(), ct))
                    .ThrowsAsync(new AlreadyExistsException());
        cacheMock.Setup(cache => cache.FetchAsync(manifestDesc, ct)).ReturnsAsync(expectedStream);
        sourceMock.Setup(source => source.FetchAsync(manifestDesc, ct)).ReturnsAsync(expectedStream);


        var proxy = new Proxy { Cache = cacheMock.Object, Source = sourceMock.Object };

       var result = await proxy.FetchAsync(manifestDesc, ct);
       Assert.Same(expectedStream, result);
       sourceMock.Verify(source => source.FetchAsync(manifestDesc, ct), Times.Once);
       cacheMock.Verify(cache => cache.PushAsync(manifestDesc, It.IsAny<Stream>(), ct), Times.Once);
       cacheMock.Verify(cache => cache.FetchAsync(manifestDesc, ct), Times.Once);
       cacheMock.Verify(cache => cache.ExistsAsync(manifestDesc, ct), Times.Once);
    }
    
    [Fact]
    public async Task FetchAsync_SourceImplementsIReferenceFetchable_ManifestType_CachesAndReturnsCacheStream()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        var originalStream = new MemoryStream(data);
        var cachedStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var storageMock = new Mock<IStorage>();
        storageMock
            .Setup(s => s.PushAsync(descriptor, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        storageMock
            .Setup(s => s.FetchAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedStream)
            .Verifiable();

        var sourceMock = new Mock<ITarget>();
        var srcRefMock = sourceMock.As<IReferenceFetchable>();
        srcRefMock
            .Setup(s => s.FetchAsync("ref", It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, (Stream)originalStream))
            .Verifiable();

        var proxy = new Proxy { Cache = storageMock.Object, Source = sourceMock.Object };

        // Act
        var (desc, resultStream) = await proxy.FetchAsync("ref", CancellationToken.None);

        // Assert
        Assert.Equal(descriptor, desc);
        Assert.Same(cachedStream, resultStream);
        srcRefMock.Verify();
        storageMock.Verify();
    }
    
    [Fact]
    public async Task FetchAsync_SourceDoesNotImplementIReferenceFetchable_NonManifestType_DoesNotCache_ReturnsSourceStream()
    {
        // Arrange
        var data = new byte[] { 5, 6, 7 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        var sourceStream = new MemoryStream(data);

        var storageMock = new Mock<IStorage>(MockBehavior.Strict);
        // No setup on PushAsync or FetchAsync => should not be called

        var sourceMock = new Mock<ITarget>();
        sourceMock
            .Setup(s => s.ResolveAsync("myref", It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor)
            .Verifiable();
        sourceMock
            .Setup(s => s.FetchAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream)sourceStream)
            .Verifiable();

        var proxy = new Proxy { Cache = storageMock.Object, Source = sourceMock.Object };

        // Act
        var (desc, resultStream) = await proxy.FetchAsync("myref", CancellationToken.None);

        // Assert
        Assert.Equal(descriptor, desc);
        Assert.Same(sourceStream, resultStream);
        sourceMock.Verify();
    }
    
    [Fact]
    public async Task FetchAsync_SourceDoesNotImplementIReferenceFetchable_ManifestType_CachesAndReturnsCacheStream()
    {
        // Arrange
        var data = new byte[] { 9, 8, 7 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        var sourceStream = new MemoryStream(data);
        var cachedStream = new MemoryStream(new byte[] { 4, 3, 2 });

        var storageMock = new Mock<IStorage>();
        storageMock
            .Setup(s => s.PushAsync(descriptor, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        storageMock
            .Setup(s => s.FetchAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedStream)
            .Verifiable();

        var sourceMock = new Mock<ITarget>();
        sourceMock
            .Setup(s => s.ResolveAsync("idx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor)
            .Verifiable();
        sourceMock
            .Setup(s => s.FetchAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream)sourceStream)
            .Verifiable();

        var proxy = new Proxy { Cache = storageMock.Object, Source = sourceMock.Object };

        // Act
        var (desc, resultStream) = await proxy.FetchAsync("idx", CancellationToken.None);

        // Assert
        Assert.Equal(descriptor, desc);
        Assert.Same(cachedStream, resultStream);
        sourceMock.Verify();
        storageMock.Verify();
    }
    
    
    [Fact]
    public async Task FetchAsync_CachePushThrowsAlreadyExists_ReturnsCacheFetchStream()
    {
        // Arrange
        var data = new byte[] { 11, 12, 13 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        var sourceStream = new MemoryStream(data);
        var cachedStream = new MemoryStream(new byte[] { 14, 15, 16 });

        var storageMock = new Mock<IStorage>();
        storageMock.Setup(s => s.PushAsync(descriptor, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlreadyExistsException())
            .Verifiable();
        storageMock.Setup(s => s.FetchAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedStream)
            .Verifiable();

        var sourceMock = new Mock<ITarget>();
        var srcRefMock = sourceMock.As<IReferenceFetchable>();
        srcRefMock
            .Setup(s => s.FetchAsync("dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, (Stream)sourceStream))
            .Verifiable();

        var proxy = new Proxy { Cache = storageMock.Object, Source = sourceMock.Object };

        // Act
        var (desc, resultStream) = await proxy.FetchAsync("dup", CancellationToken.None);

        // Assert
        Assert.Equal(descriptor, desc);
        Assert.Same(cachedStream, resultStream);
        srcRefMock.Verify();
        storageMock.Verify();
    }
}
