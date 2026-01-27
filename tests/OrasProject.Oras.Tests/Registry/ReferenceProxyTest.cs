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
using OrasProject.Oras.Registry;

namespace OrasProject.Oras.Tests.Registry;

public class ReferenceProxyTest
{
    [Fact]
    public async Task FetchAsync_FetchContentByReference_CachingSkipped()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        using var stream = new MemoryStream(data);

        var storageMock = new Mock<IStorage>();
        storageMock
            .Setup(s => s.ExistsAsync(descriptor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var sourceMock = new Mock<ITarget>();
        var srcRefMock = sourceMock.As<IReferenceFetchable>();
        srcRefMock
            .Setup(s => s.FetchAsync("ref", It.IsAny<FetchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, (Stream)stream))
            .Verifiable();

        var proxy = new Proxy { Cache = storageMock.Object, Source = sourceMock.Object };
        var referenceProxy = new ReferenceProxy(srcRefMock.Object, proxy);

        // Content already exists in Cache
        var exists = await proxy.Cache.ExistsAsync(descriptor);
        Assert.True(exists);

        // Fetch by reference
        var (desc, resultStream) = await referenceProxy.FetchAsync("ref", CancellationToken.None);

        // Assert
        exists = await proxy.Cache.ExistsAsync(descriptor);
        Assert.True(exists);
        Assert.Equal(descriptor, desc);
        Assert.Same(stream, resultStream);
        srcRefMock.Verify();
        storageMock.Verify();
    }

    [Fact]
    public async Task FetchAsync_FetchContentByReference_DoCaching()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        using var stream = new MemoryStream(data);

        var sourceMock = new Mock<ITarget>();
        var srcRefMock = sourceMock.As<IReferenceFetchable>();
        srcRefMock
            .Setup(s => s.FetchAsync("ref", It.IsAny<FetchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, (Stream)stream))
            .Verifiable();

        var proxy = new Proxy { Cache = new MemoryStorage(), Source = sourceMock.Object };
        var referenceProxy = new ReferenceProxy(srcRefMock.Object, proxy);

        // Content does not exist in cache
        var exists = await proxy.Cache.ExistsAsync(descriptor);
        Assert.False(exists);

        // Fetch by reference
        var (desc, resultStream) = await referenceProxy.FetchAsync("ref", CancellationToken.None);

        // Assert
        exists = await proxy.Cache.ExistsAsync(descriptor);
        Assert.True(exists);
        Assert.Equal(descriptor, desc);
        srcRefMock.Verify();

        // Assert that the returned stream contains the expected content
        using (var actualBytes = new MemoryStream())
        {
            await resultStream.CopyToAsync(actualBytes, CancellationToken.None);
            Assert.Equal(data, actualBytes.ToArray());
        }

        // Fetch from the cache
        resultStream = await referenceProxy.FetchAsync(desc, CancellationToken.None);

        // Assert that the returned stream contains the expected content
        using (var actualBytes = new MemoryStream())
        {
            await resultStream.CopyToAsync(actualBytes, CancellationToken.None);
            Assert.Equal(data, actualBytes.ToArray());
        }

        // Assert that the content exists in the reference proxy
        exists = await referenceProxy.ExistsAsync(desc, CancellationToken.None);
        Assert.True(exists);
    }

    [Fact]
    public async Task FetchAsync_FetchContentByReference_WithOptions()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(data),
            Size = data.Length
        };
        using var stream = new MemoryStream(data);

        var options = new FetchOptions
        {
            Headers = new Dictionary<string, IEnumerable<string>>
            {
                { "X-Custom-Header", new[] { "custom-value" } }
            }
        };

        var sourceMock = new Mock<ITarget>();
        var srcRefMock = sourceMock.As<IReferenceFetchable>();
        srcRefMock
            .Setup(s => s.FetchAsync("ref", options, It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, (Stream)stream))
            .Verifiable();

        var proxy = new Proxy { Cache = new MemoryStorage(), Source = sourceMock.Object };
        var referenceProxy = new ReferenceProxy(srcRefMock.Object, proxy);

        // Fetch by reference with options
        var (desc, resultStream) = await referenceProxy.FetchAsync("ref", options, CancellationToken.None);

        // Assert
        var exists = await proxy.Cache.ExistsAsync(descriptor);
        Assert.True(exists);
        Assert.Equal(descriptor, desc);
        srcRefMock.Verify();
    }
}
