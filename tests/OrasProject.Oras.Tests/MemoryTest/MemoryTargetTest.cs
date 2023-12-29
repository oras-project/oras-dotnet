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

using OrasProject.Oras.Constants;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Memory;
using OrasProject.Oras.Oci;
using System.Text;
using System.Text.Json;
using Xunit;
using static OrasProject.Oras.Content.Content;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.MemoryTest
{
    public class MemoryTargetTest
    {
        /// <summary>
        /// This method tests if a MemoryTarget object can store data
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CanStoreData()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            string hash = CalculateDigest(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var reference = "foobar";
            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(content);
            await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            await memoryTarget.TagAsync(descriptor, reference, cancellationToken);
            var gotDescriptor = await memoryTarget.ResolveAsync(reference, cancellationToken);

            Assert.Equal(descriptor, gotDescriptor);
            Assert.True(await memoryTarget.ExistsAsync(descriptor, cancellationToken));

            var readContent = await memoryTarget.FetchAsync(descriptor, cancellationToken);
            using var memoryStream = new MemoryStream();
            readContent.CopyTo(memoryStream);

            // Assert that the fetched content is equal to the original content
            Assert.Equal(content, memoryStream.ToArray());
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to fetch a non-existing descriptor
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ThrowsNotFoundExceptionWhenDataIsNotAvailable()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");

            string hash = CalculateDigest(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var contentExists = await memoryTarget.ExistsAsync(descriptor, cancellationToken);
            Assert.False(contentExists);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await memoryTarget.FetchAsync(descriptor, cancellationToken);
            });
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to push an already existing data
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ThrowsAlreadyExistsExceptionWhenSameDataIsPushedTwice()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            string hash = CalculateDigest(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(content);
            await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            await Assert.ThrowsAsync<AlreadyExistsException>(() => memoryTarget.PushAsync(descriptor, stream, cancellationToken));
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to push an artifact with a wrong descriptor
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ThrowsAnErrorWhenABadPushOccurs()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            var wrongContent = Encoding.UTF8.GetBytes("Hello World!");
            string hash = CalculateDigest(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(wrongContent);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            });
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws a MismatchedDigestException when trying to push an artifact
        /// that has a different digest on the descriptor compared to the digest of the content
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ThrowsMismatchedDigestExceptionWhenHashInDigestIsDifferentFromContentDigest()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            var wrongContent = Encoding.UTF8.GetBytes("Hello Danny");
            string hash = CalculateDigest(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(wrongContent);
            await Assert.ThrowsAnyAsync<MismatchedDigestException>(async () =>
            {
                await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            });
        }

        /// <summary>
        /// This method tests if a MemoryTarget object can return the predecesors of a descriptor
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldReturnPredecessorsOfNodes()
        {
            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var blobs = new List<byte[]>();
            var descs = new List<Descriptor>();
            var appendBlob = (string mediaType, byte[] blob) =>
            {
                blobs.Add(blob);
                var desc = new Descriptor
                {
                    MediaType = mediaType,
                    Digest = CalculateDigest(blob),
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
                appendBlob(OCIMediaTypes.ImageManifest, manifestBytes);
            };

            var generateIndex = (List<Descriptor> manifests) =>
            {
                var index = new Index
                {
                    Manifests = manifests
                };
                var indexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(index));
                appendBlob(OCIMediaTypes.ImageIndex, indexBytes);
            };
            var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
            appendBlob(OCIMediaTypes.ImageConfig, getBytes("config")); // blob 0
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("foo")); // blob 1
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("bar")); // blob 2
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("hello")); // blob 3
            generateManifest(descs[0], descs.GetRange(1, 2)); // blob 4
            generateManifest(descs[0], new() { descs[3] }); // blob 5
            generateManifest(descs[0], descs.GetRange(1, 3)); // blob 6
            generateIndex(descs.GetRange(4, 2)); // blob 7
            generateIndex(new() { descs[6] }); // blob 8

            for (var i = 0; i < blobs.Count; i++)
            {
                await memoryTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

            }
            var wants = new List<List<Descriptor>>()
            {
                descs.GetRange(4, 3), // blob 0
                new() { descs[4], descs[6] }, // blob 1
                new() { descs[4], descs[6] }, // blob 2
                new() { descs[5], descs[6] }, // blob 3
                new() { descs[7] }, // blob 4
                new() { descs[7] }, // blob 5
                new() { descs[8] }, // blob 6
                null!, // blob 7
                null! // blob 8
            };


            foreach (var (i, want) in wants.Select((v, i) => (i, v)))
            {
                var predecessors = await memoryTarget.PredecessorsAsync(descs[i], cancellationToken);
                if (predecessors is null && want is null) continue;
                want.Sort((a, b) => (int)b.Size - (int)a.Size);
                predecessors?.Sort((a, b) => (int)b.Size - (int)a.Size);
                Assert.Equal(predecessors, want);
            }
        }
    }
}
