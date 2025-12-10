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
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests;

public class ExtendedCopyTest
{
    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with referrers from source to destination.
    /// Tests the basic ExtendedCopyGraphAsync functionality with a simple graph structure.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithReferrers()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers, Descriptor? subject = null)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers,
                Subject = subject
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create base artifact
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (base manifest)

        // Create referrer (e.g., signature)
        AppendBlob(MediaType.ImageConfig, GetBytes("sig-config")); // blob 3
        AppendBlob(MediaType.ImageLayer, GetBytes("signature")); // blob 4
        GenerateManifest(descs[3], descs.GetRange(4, 1), descs[2]); // blob 5 (signature manifest)

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var node = descs[2]; // base manifest
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions();

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify all content was copied
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }

    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with depth limit.
    /// Tests ExtendedCopyGraphAsync with the Depth option set.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithDepthLimit()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers, Descriptor? subject = null)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers,
                Subject = subject
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create base artifact (depth 0)
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (base manifest)

        // Create first referrer (depth 1)
        AppendBlob(MediaType.ImageConfig, GetBytes("ref1-config")); // blob 3
        AppendBlob(MediaType.ImageLayer, GetBytes("ref1-layer")); // blob 4
        GenerateManifest(descs[3], descs.GetRange(4, 1), descs[2]); // blob 5

        // Create second referrer (depth 2)
        AppendBlob(MediaType.ImageConfig, GetBytes("ref2-config")); // blob 6
        AppendBlob(MediaType.ImageLayer, GetBytes("ref2-layer")); // blob 7
        GenerateManifest(descs[6], descs.GetRange(7, 1), descs[5]); // blob 8

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            using var ms = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], ms, cancellationToken);
        }

        var node = descs[2]; // base manifest
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions
        {
            Depth = 1 // Only copy up to depth 1
        };

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify base artifact and first referrer were copied
        for (var i = 0; i <= 5; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
        }

        // Verify second referrer was NOT copied (depth 2)
        for (var i = 6; i < descs.Count; i++)
        {
            Assert.False(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
        }
    }

    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with custom FindPredecessors function.
    /// Tests ExtendedCopyGraphAsync with a custom predecessor finder.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithCustomFindPredecessors()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var findPredecessorsCalled = 0;

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers, Descriptor? subject = null)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers,
                Subject = subject
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create base artifact
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (base manifest)

        // Create referrer
        AppendBlob(MediaType.ImageConfig, GetBytes("ref-config")); // blob 3
        AppendBlob(MediaType.ImageLayer, GetBytes("ref-layer")); // blob 4
        GenerateManifest(descs[3], descs.GetRange(4, 1), descs[2]); // blob 5

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var node = descs[2]; // base manifest
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions
        {
            FindPredecessors = async (src, desc, ct) =>
            {
                Interlocked.Increment(ref findPredecessorsCalled);
                return await src.GetPredecessorsAsync(desc, ct);
            }
        };

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify custom FindPredecessors was called
        Assert.True(findPredecessorsCalled > 0);

        // Verify all content was copied
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
        }
    }

    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with multiple referrers at the same level.
    /// Tests ExtendedCopyGraphAsync with multiple predecessors.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithMultipleReferrers()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers, Descriptor? subject = null)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers,
                Subject = subject
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create base artifact
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (base manifest)

        // Create first referrer (signature)
        AppendBlob(MediaType.ImageConfig, GetBytes("sig-config")); // blob 3
        AppendBlob(MediaType.ImageLayer, GetBytes("signature")); // blob 4
        GenerateManifest(descs[3], descs.GetRange(4, 1), descs[2]); // blob 5

        // Create second referrer (SBOM)
        AppendBlob(MediaType.ImageConfig, GetBytes("sbom-config")); // blob 6
        AppendBlob(MediaType.ImageLayer, GetBytes("sbom-data")); // blob 7
        GenerateManifest(descs[6], descs.GetRange(7, 1), descs[2]); // blob 8

        // Create third referrer (provenance)
        AppendBlob(MediaType.ImageConfig, GetBytes("prov-config")); // blob 9
        AppendBlob(MediaType.ImageLayer, GetBytes("prov-data")); // blob 10
        GenerateManifest(descs[9], descs.GetRange(10, 1), descs[2]); // blob 11

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            using var ms = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], ms, cancellationToken);
        }

        var node = descs[2]; // base manifest
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions();

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify all content was copied
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }

    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with ExtendedCopyGraphOptions callbacks.
    /// Tests ExtendedCopyGraphAsync with PreCopyAsync, PostCopyAsync, and OnCopySkippedAsync.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithCallbacks()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var preCopyCount = 0;
        var postCopyCount = 0;
        var copySkipCount = 0;

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers, Descriptor? subject = null)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers,
                Subject = subject
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create base artifact
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (base manifest)

        // Create referrer
        AppendBlob(MediaType.ImageConfig, GetBytes("ref-config")); // blob 3
        AppendBlob(MediaType.ImageLayer, GetBytes("ref-layer")); // blob 4
        GenerateManifest(descs[3], descs.GetRange(4, 1), descs[2]); // blob 5

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            await using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var node = descs[2]; // base manifest
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions
        {
            Concurrency = 2,
            MaxMetadataBytes = 1024 * 1024,
            PreCopyAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref preCopyCount);
                return Task.FromResult(CopyNodeDecision.Continue);
            },
            PostCopyAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref postCopyCount);
                return Task.CompletedTask;
            },
            OnCopySkippedAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref copySkipCount);
                return Task.CompletedTask;
            }
        };

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify callbacks were invoked
        Assert.True(preCopyCount > 0);
        Assert.True(postCopyCount > 0);

        // Do another copy to trigger OnCopySkipped
        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);
        Assert.True(copySkipCount > 0);

        // Verify all content was copied
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
        }
    }

    /// <summary>
    /// Can copy a directed acyclic graph (DAG) with no predecessors (leaf node).
    /// Tests ExtendedCopyGraphAsync when the node has no referrers.
    /// </summary>
    [Fact]
    public async Task CanExtendedCopyGraphWithNoPredecessors()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        // Create simple artifact with no referrers
        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("layer1")); // blob 1
        GenerateManifest(descs[0], descs.GetRange(1, 1)); // blob 2 (manifest)

        // Push all blobs to source
        for (var i = 0; i < blobs.Count; i++)
        {
            using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var node = descs[2]; // manifest with no predecessors
        var destinationTarget = new MemoryStore();
        var opts = new ExtendedCopyGraphOptions();

        await sourceTarget.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken);

        // Verify all content was copied
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }

    /// <summary>
    /// ExtendedCopyGraphAsync throws when source storage is null.
    /// </summary>
    [Fact]
    public async Task ExtendedCopyGraphAsync_SrcIsNull_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        IReadOnlyGraphStorage? sourceTarget = null;
        var destinationTarget = new MemoryStore();
        var node = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = "sha256:1234567890abcdef",
            Size = 100
        };
        var opts = new ExtendedCopyGraphOptions();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sourceTarget!.ExtendedCopyGraphAsync(destinationTarget, node, opts, cancellationToken));
    }

    /// <summary>
    /// ExtendedCopyGraphAsync throws when destination storage is null.
    /// </summary>
    [Fact]
    public async Task ExtendedCopyGraphAsync_DstIsNull_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        var sourceTarget = new MemoryStore();
        IStorage? destinationTarget = null;
        var node = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = "sha256:1234567890abcdef",
            Size = 100
        };
        var opts = new ExtendedCopyGraphOptions();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sourceTarget.ExtendedCopyGraphAsync(destinationTarget!, node, opts, cancellationToken));
    }
}
