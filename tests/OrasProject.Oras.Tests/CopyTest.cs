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
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests;

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

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var reference = "foobar";
        await sourceTarget.TagAsync(root, reference, cancellationToken);
        var destinationTarget = new MemoryStore();
        var gotDesc = await sourceTarget.CopyAsync(reference, destinationTarget, "", cancellationToken);
        Assert.Equal(gotDesc, root);
        Assert.Equal(await destinationTarget.ResolveAsync(reference, cancellationToken), root);

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
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyBetweenMemoryTargets()
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

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new MemoryStore();
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = sourceTarget
        };
        await sourceTarget.CopyGraphAsync(destinationTarget, root, proxy, new CopyGraphOptions(), cancellationToken);
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
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target
    /// with customized CopyOptions.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyWithCopyOptions()
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

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("foo")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 1

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[0];
        var reference = "foo";
        await sourceTarget.TagAsync(root, reference, cancellationToken);

        var destinationTarget = new MemoryStore();
        var copyOptions = new CopyOptions()
        {
            MapRoot = (_, _, _) => Task.FromResult(descs[1])
        };
        await sourceTarget.CopyAsync(reference, destinationTarget, "bar", copyOptions, cancellationToken);
        Assert.True(await destinationTarget.ExistsAsync(descs[1], cancellationToken));
        Assert.False(await destinationTarget.ExistsAsync(descs[0], cancellationToken));
    }

    /// <summary>
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target
    /// with customized CopyGraphOptions.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyWithCopyGraphOptions()
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

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new MemoryStore();
        var copyGraphOptions = new CopyGraphOptions
        {
            Concurrency = 3,
            MaxMetadataBytes = 2 * 1024 * 1024,
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

        Assert.Equal(3, copyGraphOptions.Concurrency);
        Assert.Equal(2 * 1024 * 1024, copyGraphOptions.MaxMetadataBytes);
        await sourceTarget.CopyGraphAsync(destinationTarget, root, copyGraphOptions, cancellationToken);
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }

        // do another copy to trigger OnCopySkipped
        await sourceTarget.CopyGraphAsync(destinationTarget, root, copyGraphOptions, cancellationToken);
        Assert.Equal(4, preCopyCount);
        Assert.Equal(4, postCopyCount);
        Assert.Equal(1, copySkipCount);
    }

    [Fact]
    public void CopyGraphOptions_ConcurrencyZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.Concurrency = 0);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("Concurrency must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_ConcurrencyNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.Concurrency = -1);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("Concurrency must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_MaxMetadataBytesZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxMetadataBytes = 0);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("MaxMetadataBytes must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_MaxMetadataBytesNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxMetadataBytes = -1);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("MaxMetadataBytes must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task CopyAsync_SrcRefIsNull_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        var sourceTarget = new MemoryStore();
        var destinationTarget = new MemoryStore();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sourceTarget.CopyAsync("", destinationTarget, "", cancellationToken));
    }

    [Fact]
    public async Task CopyGraphAsync_DescIsInvalid_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        var sourceTarget = new MemoryStore();
        var destinationTarget = new MemoryStore();
        var invalidDesc = new Descriptor()
        {
            MediaType = MediaType.ImageConfig,
            Digest = ""
        };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sourceTarget.CopyGraphAsync(destinationTarget, invalidDesc, cancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CopyAsync_WaitsForSharedLayer_WhenPlatformManifestsShareLayerAsync(int concurrency)
    {
        await CopySharedLayerGraphAsync(prepopulateLayer: false, concurrency);
    }

    [Fact]
    public async Task CopyAsync_CopiesSharedLayerGraph_WhenLayerAlreadyExistsAsync()
    {
        await CopySharedLayerGraphAsync(prepopulateLayer: true);
    }

    [Fact]
    public async Task CopyGraphAsync_CopiesSharedLayerGraph_WhenOptionsAreOmittedAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new MemoryStore();

        await graph.Source.CopyGraphAsync(destination, graph.Root, deadline.Token);

        Assert.True(await destination.ExistsAsync(graph.Root, deadline.Token));
        Assert.True(await destination.ExistsAsync(graph.Layer, deadline.Token));
        foreach (var manifest in graph.Manifests)
        {
            Assert.True(await destination.ExistsAsync(manifest, deadline.Token));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtendedCopyGraphAsync_CopiesSharedLayerOnce_WhenPredecessorsOverlapAsync(
        bool duplicatePredecessors)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new DependencyCheckingTarget(graph.Layer);
        var options = new ExtendedCopyGraphOptions { Depth = duplicatePredecessors ? 0 : 1 };
        if (duplicatePredecessors)
        {
            options.FindPredecessors = async (storage, node, cancellationToken) =>
            {
                var predecessors = (await storage.GetPredecessorsAsync(node, cancellationToken)).ToArray();
                return predecessors.Concat(predecessors);
            };
        }

        var copy = graph.Source.ExtendedCopyGraphAsync(
            destination, graph.Layer, options, deadline.Token);
        await CompleteGatedCopyAsync(copy, destination, deadline.Token);

        foreach (var manifest in graph.Manifests)
        {
            Assert.True(await destination.ExistsAsync(manifest, deadline.Token));
        }
        Assert.Equal(duplicatePredecessors,
            await destination.ExistsAsync(graph.Root, deadline.Token));
        Assert.Equal(1, destination.SharedLayerUploadCount);
        Assert.False(destination.PrematureManifestAttempt.Task.IsCompleted);
    }

    [Fact]
    public async Task CopyAsync_PropagatesSharedLayerFailure_WithoutPublishingDependentManifestsAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var failure = new IOException("Shared layer upload failed.");
        var destination = new DependencyCheckingTarget(graph.Layer) { LayerUploadException = failure };
        var copy = graph.Source.CopyAsync(
            "image", destination, "copied", new CopyOptions(), deadline.Token);

        var actual = await Assert.ThrowsAsync<IOException>(async () =>
        {
            try
            {
                await destination.LayerUploadStarted.Task.WaitAsync(deadline.Token);
            }
            finally
            {
                destination.ReleaseLayerUpload();
                await copy;
            }
        });

        Assert.Same(failure, actual);
        Assert.Equal(1, destination.SharedLayerUploadCount);
        Assert.False(destination.PrematureManifestAttempt.Task.IsCompleted);
        Assert.False(await destination.ExistsAsync(graph.Root, deadline.Token));
        foreach (var manifest in graph.Manifests)
        {
            Assert.False(await destination.ExistsAsync(manifest, deadline.Token));
        }
    }

    [Theory]
    [InlineData("find")]
    [InlineData("pre")]
    [InlineData("post")]
    public async Task CopyAsync_PropagatesSharedCallbackFailure_WithoutPublishingDependentManifestsAsync(
        string callback)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new DependencyCheckingTarget(graph.Layer);
        destination.ReleaseLayerUpload();
        var failure = new IOException("Shared layer callback failed.");
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task FailSharedLayerAsync(Descriptor node, CancellationToken cancellationToken)
        {
            if (node.Digest == graph.Layer.Digest)
            {
                callbackStarted.TrySetResult();
                await releaseCallback.Task.WaitAsync(cancellationToken);
                throw failure;
            }
        }

        var options = new CopyOptions();
        if (callback == "find")
        {
            options.FindSuccessorsAsync = async (storage, node, cancellationToken) =>
            {
                await FailSharedLayerAsync(node, cancellationToken);
                return await storage.GetSuccessorsAsync(node, cancellationToken);
            };
        }
        else if (callback == "pre")
        {
            options.PreCopyAsync = async (node, cancellationToken) =>
            {
                await FailSharedLayerAsync(node, cancellationToken);
                return CopyNodeDecision.Continue;
            };
        }
        else
        {
            options.PostCopyAsync = FailSharedLayerAsync;
        }
        var copy = graph.Source.CopyAsync("image", destination, "copied", options, deadline.Token);

        var actual = await Assert.ThrowsAsync<IOException>(async () =>
        {
            try
            {
                await callbackStarted.Task.WaitAsync(deadline.Token);
            }
            finally
            {
                releaseCallback.TrySetResult();
                await copy;
            }
        });

        Assert.Same(failure, actual);
        Assert.True(callbackStarted.Task.IsCompletedSuccessfully);
        Assert.False(destination.PrematureManifestAttempt.Task.IsCompleted);
        Assert.False(await destination.ExistsAsync(graph.Root, deadline.Token));
        foreach (var manifest in graph.Manifests)
        {
            Assert.False(await destination.ExistsAsync(manifest, deadline.Token));
        }
    }

    [Fact]
    public async Task CopyAsync_CancelsSharedLayerWaiters_WithoutPublishingDependentManifestsAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new DependencyCheckingTarget(graph.Layer);
        var copy = graph.Source.CopyAsync(
            "image", destination, "copied", new CopyOptions(), cancellation.Token);
        var uploadStarted = false;

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            try
            {
                await destination.LayerUploadStarted.Task.WaitAsync(deadline.Token);
                uploadStarted = true;
            }
            finally
            {
                await cancellation.CancelAsync();
                destination.ReleaseLayerUpload();
                await copy;
            }
        });

        Assert.True(uploadStarted);
        Assert.False(deadline.IsCancellationRequested);
        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.True(copy.IsCanceled);
        Assert.Equal(1, destination.SharedLayerUploadCount);
        Assert.False(destination.PrematureManifestAttempt.Task.IsCompleted);
        foreach (var manifest in graph.Manifests)
        {
            Assert.False(await destination.ExistsAsync(manifest, deadline.Token));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyAsync_InvokesDuplicateSkipCallback_AfterSharedLayerCompletionAsync(
        bool preCopySkipsLayer)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new DependencyCheckingTarget(graph.Layer);
        var preCopyCount = 0;
        var postCopyCount = 0;
        var skippedCount = 0;
        var options = new CopyOptions
        {
            PreCopyAsync = async (node, cancellationToken) =>
            {
                if (node.Digest == graph.Layer.Digest)
                {
                    Interlocked.Increment(ref preCopyCount);
                    if (preCopySkipsLayer)
                    {
                        destination.ReleaseLayerUpload();
                        using var content = await graph.Source.FetchAsync(node, cancellationToken);
                        await destination.PushAsync(node, content, cancellationToken);
                        return CopyNodeDecision.SkipNode;
                    }
                }
                return CopyNodeDecision.Continue;
            },
            PostCopyAsync = (node, _) =>
            {
                if (node.Digest == graph.Layer.Digest)
                {
                    Interlocked.Increment(ref postCopyCount);
                }
                return Task.CompletedTask;
            },
            OnCopySkippedAsync = async (node, cancellationToken) =>
            {
                if (node.Digest == graph.Layer.Digest)
                {
                    Assert.True(await destination.ExistsAsync(node, cancellationToken));
                    Assert.Equal(preCopySkipsLayer ? 0 : 1, Volatile.Read(ref postCopyCount));
                    Interlocked.Increment(ref skippedCount);
                }
            }
        };
        var copy = graph.Source.CopyAsync("image", destination, "copied", options, deadline.Token);
        await CompleteGatedCopyAsync(copy, destination, deadline.Token);

        Assert.Equal(graph.Root, await copy);
        Assert.Equal(graph.Root, await destination.ResolveAsync("copied", deadline.Token));
        Assert.Equal(1, preCopyCount);
        Assert.Equal(preCopySkipsLayer ? 0 : 1, postCopyCount);
        Assert.Equal(1, skippedCount);
        Assert.Equal(1, destination.SharedLayerUploadCount);
    }

    private static async Task CopySharedLayerGraphAsync(bool prepopulateLayer, int concurrency = 3)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = deadline.Token;
        var graph = await CreateSharedLayerGraphAsync(cancellationToken);
        var destination = new DependencyCheckingTarget(graph.Layer);
        if (prepopulateLayer)
        {
            destination.ReleaseLayerUpload();
            using var content = await graph.Source.FetchAsync(graph.Layer, cancellationToken);
            await destination.PushAsync(graph.Layer, content, cancellationToken);
        }
        var copy = graph.Source.CopyAsync("image", destination, "copied",
            new CopyOptions { Concurrency = concurrency }, cancellationToken);
        await CompleteGatedCopyAsync(copy, destination, cancellationToken);

        Assert.Equal(graph.Root, await copy);
        Assert.Equal(graph.Root, await destination.ResolveAsync("copied", cancellationToken));
        Assert.Equal(1, destination.SharedLayerUploadCount);
        Assert.False(destination.PrematureManifestAttempt.Task.IsCompleted);
    }

    private static async Task CompleteGatedCopyAsync(
        Task copy, DependencyCheckingTarget destination, CancellationToken cancellationToken)
    {
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var observationEnded = Task.Delay(TimeSpan.FromSeconds(5), observation.Token);
        try
        {
            await destination.LayerUploadStarted.Task.WaitAsync(cancellationToken);
            // A premature push releases the gate promptly on buggy implementations.
            // The observation bound also releases it when a correct implementation waits.
            await Task.WhenAny(destination.PrematureManifestAttempt.Task, copy, observationEnded)
                .WaitAsync(cancellationToken);
        }
        finally
        {
            destination.ReleaseLayerUpload();
            await observation.CancelAsync();
            await copy;
        }
    }

    private static async Task<(MemoryStore Source, Descriptor Root, Descriptor Layer,
        IReadOnlyList<Descriptor> Manifests)> CreateSharedLayerGraphAsync(
        CancellationToken cancellationToken)
    {
        var source = new MemoryStore();
        using var layerContent = new MemoryStream(Encoding.UTF8.GetBytes("shared content\n"));
        using var layerArchive = new MemoryStream();
        using (var writer = new TarWriter(layerArchive, TarEntryFormat.Ustar, leaveOpen: true))
        {
            await writer.WriteEntryAsync(new UstarTarEntry(TarEntryType.RegularFile, "hello.txt")
            {
                DataStream = layerContent,
                ModificationTime = DateTimeOffset.UnixEpoch
            }, cancellationToken);
        }
        var layer = await PushContentAsync(
            source, layerArchive.ToArray(), MediaType.ImageLayer, cancellationToken);
        var manifests = new List<Descriptor>();
        foreach (var architecture in new[] { "amd64", "arm64" })
        {
            var configContent = JsonSerializer.SerializeToUtf8Bytes(new
            {
                architecture,
                os = "linux",
                rootfs = new { type = "layers", diff_ids = new[] { layer.Digest } }
            });
            var config = await PushContentAsync(
                source, configContent, MediaType.ImageConfig, cancellationToken);
            var manifest = new Manifest
            {
                SchemaVersion = 2,
                MediaType = MediaType.ImageManifest,
                Config = config,
                Layers = new[] { layer }
            };
            manifests.Add(await PushContentAsync(source, JsonSerializer.SerializeToUtf8Bytes(manifest),
                MediaType.ImageManifest, cancellationToken));
        }
        var index = new OrasProject.Oras.Oci.Index
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageIndex,
            Manifests = manifests
        };
        var root = await PushContentAsync(source, JsonSerializer.SerializeToUtf8Bytes(index),
            MediaType.ImageIndex, cancellationToken);
        await source.TagAsync(root, "image", cancellationToken);

        return (source, root, layer, manifests);
    }

    private static async Task<Descriptor> PushContentAsync(
        MemoryStore store, byte[] content, string mediaType, CancellationToken cancellationToken)
    {
        var descriptor = Descriptor.Create(content, mediaType);
        using var stream = new MemoryStream(content, writable: false);
        await store.PushAsync(descriptor, stream, cancellationToken);
        return descriptor;
    }

    private sealed class DependencyCheckingTarget(Descriptor sharedLayer) : ITarget
    {
        private readonly MemoryStore _store = new();
        private readonly TaskCompletionSource _releaseLayerUpload =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sharedLayerUploadCount;

        public int SharedLayerUploadCount => Volatile.Read(ref _sharedLayerUploadCount);

        public Exception? LayerUploadException { get; init; }

        public TaskCompletionSource LayerUploadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PrematureManifestAttempt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseLayerUpload() => _releaseLayerUpload.TrySetResult();

        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default) =>
            _store.ExistsAsync(target, cancellationToken);

        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default) =>
            _store.FetchAsync(target, cancellationToken);

        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default) =>
            _store.ResolveAsync(reference, cancellationToken);

        public Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default) =>
            _store.TagAsync(descriptor, reference, cancellationToken);

        public async Task PushAsync(
            Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        {
            if (expected.Digest == sharedLayer.Digest)
            {
                Interlocked.Increment(ref _sharedLayerUploadCount);
                LayerUploadStarted.TrySetResult();
                await _releaseLayerUpload.Task.WaitAsync(cancellationToken);
                if (LayerUploadException is { } exception)
                {
                    throw exception;
                }
            }

            using var buffered = new MemoryStream();
            await content.CopyToAsync(buffered, cancellationToken);
            IEnumerable<Descriptor> dependencies = Array.Empty<Descriptor>();
            if (expected.MediaType == MediaType.ImageManifest)
            {
                var manifest = JsonSerializer.Deserialize<Manifest>(buffered.ToArray())
                    ?? throw new InvalidOperationException("Expected an image manifest.");
                dependencies = manifest.Layers.Prepend(manifest.Config);
            }
            else if (expected.MediaType == MediaType.ImageIndex)
            {
                var index = JsonSerializer.Deserialize<OrasProject.Oras.Oci.Index>(buffered.ToArray())
                    ?? throw new InvalidOperationException("Expected an image index.");
                dependencies = index.Manifests;
            }

            foreach (var dependency in dependencies)
            {
                if (!await _store.ExistsAsync(dependency, cancellationToken))
                {
                    PrematureManifestAttempt.TrySetResult();
                    throw new InvalidOperationException(
                        $"MANIFEST_BLOB_UNKNOWN (test destination): manifest {expected.Digest} " +
                        $"references uncommitted dependency {dependency.Digest}");
                }
            }
            buffered.Position = 0;
            await _store.PushAsync(expected, buffered, cancellationToken);
        }
    }
}
