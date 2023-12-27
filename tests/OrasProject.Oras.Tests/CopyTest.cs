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
using OrasProject.Oras.Memory;
using OrasProject.Oras.Models;
using System.Text;
using System.Text.Json;
using Xunit;
using static OrasProject.Oras.Content.Content;

namespace OrasProject.Oras.Tests
{
    public class CopyTest
    {
        /// <summary>
        /// Can copy a rooted directed acyclic graph (DAG) with the tagged root node
        /// in the source Memory Target to the destination Memory Target.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CanCopyBetweenMemoryTargetsWithTaggedNode()
        {
            var sourceTarget = new MemoryTarget();
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
            var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
            appendBlob(OCIMediaTypes.ImageConfig, getBytes("config")); // blob 0
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("foo")); // blob 1
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("bar")); // blob 2
            generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

            for (var i = 0; i < blobs.Count; i++)
            {
                await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

            }

            var root = descs[3];
            var reference = "foobar";
            await sourceTarget.TagAsync(root, reference, cancellationToken);
            var destinationTarget = new MemoryTarget();
            var gotDesc = await Copy.CopyAsync(sourceTarget, reference, destinationTarget, "", cancellationToken);
            Assert.Equal(gotDesc, root);
            Assert.Equal(await destinationTarget.ResolveAsync(reference, cancellationToken), root);

            for (var i = 0; i < descs.Count; i++)
            {
                Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
                var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
                var memoryStream = new MemoryStream();
                await fetchContent.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();
                Assert.Equal(blobs[i], bytes);
            }
        }

        /// <summary>
        ///  Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CanCopyBetweenMemoryTargets()
        {
            var sourceTarget = new MemoryTarget();
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
            var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
            appendBlob(OCIMediaTypes.ImageConfig, getBytes("config")); // blob 0
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("foo")); // blob 1
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("bar")); // blob 2
            generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

            for (var i = 0; i < blobs.Count; i++)
            {
                await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

            }
            var root = descs[3];
            var destinationTarget = new MemoryTarget();
            await Copy.CopyGraphAsync(sourceTarget, destinationTarget, root, cancellationToken);
            for (var i = 0; i < descs.Count; i++)
            {
                Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
                var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
                var memoryStream = new MemoryStream();
                await fetchContent.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();
                Assert.Equal(blobs[i], bytes);

            }
        }
    }
}
