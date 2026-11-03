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
using System.Collections.Concurrent;
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
    [InlineData("self", 1, false)]
    [InlineData("ancestor", 1, false)]
    [InlineData("ancestor", 3, false)]
    [InlineData("cross-branch", 2, false)]
    [InlineData("cross-branch", 3, false)]
    [InlineData("cross-branch", 2, true)]
    public async Task CopyGraphAsync_RejectsDependencyCycles_WithoutWaitingIndefinitelyAsync(
        string cycle, int concurrency, bool extendedCopy)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var a = await PushContentAsync(source, Encoding.UTF8.GetBytes("a"), MediaType.ImageConfig, deadline.Token);
        var b = await PushContentAsync(source, Encoding.UTF8.GetBytes("b"), MediaType.ImageConfig, deadline.Token);
        var root = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("root"), MediaType.ImageConfig, deadline.Token);
        var destination = new MemoryStore();
        var bothBranchesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branchesStarted = 0;
        var options = new CopyGraphOptions { Concurrency = concurrency };
        options.FindSuccessorsAsync = async (_, node, cancellationToken) =>
        {
            if (node == root)
            {
                return new[] { a, b };
            }
            if (cycle == "cross-branch")
            {
                // Ensure both nodes have independent owners before either discovers its back edge.
                if (Interlocked.Increment(ref branchesStarted) == 2)
                {
                    bothBranchesStarted.TrySetResult();
                }
                await bothBranchesStarted.Task.WaitAsync(cancellationToken);
            }
            return new[] { cycle == "self" || node == b ? a : b };
        };

        Task copy;
        if (extendedCopy)
        {
            var extendedOptions = new ExtendedCopyGraphOptions
            {
                Concurrency = concurrency,
                Depth = 1,
                FindPredecessors = (_, _, _) =>
                    Task.FromResult<IEnumerable<Descriptor>>(new[] { a, b }),
                FindSuccessorsAsync = options.FindSuccessorsAsync
            };
            copy = source.ExtendedCopyGraphAsync(destination, root, extendedOptions, deadline.Token);
        }
        else
        {
            copy = source.CopyGraphAsync(
                destination, cycle == "cross-branch" ? root : a, options, deadline.Token);
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => copy.WaitAsync(deadline.Token));

        Assert.Contains("Dependency cycle", error.Message);
        Assert.True(copy.IsFaulted);
        Assert.False(deadline.IsCancellationRequested);
        Assert.False(await destination.ExistsAsync(a, deadline.Token));
        Assert.False(await destination.ExistsAsync(b, deadline.Token));
        Assert.False(await destination.ExistsAsync(root, deadline.Token));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task CopyGraphAsync_AllowsSharedNonLeafNodes_WithoutTreatingThemAsCyclesAsync(
        int concurrency, bool duplicateSuccessors)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var a = await PushContentAsync(source, Encoding.UTF8.GetBytes("a"), MediaType.ImageConfig, deadline.Token);
        var b = await PushContentAsync(source, Encoding.UTF8.GetBytes("b"), MediaType.ImageConfig, deadline.Token);
        var leaf = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("leaf"), MediaType.ImageConfig, deadline.Token);
        var root = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("root"), MediaType.ImageConfig, deadline.Token);
        var destination = new MemoryStore();
        var options = new CopyGraphOptions
        {
            Concurrency = concurrency,
            FindSuccessorsAsync = (_, node, _) => Task.FromResult<IEnumerable<Descriptor>>(
                node == root ? new[] { a, b } :
                node == a ? Enumerable.Repeat(b, duplicateSuccessors ? 2 : 1) :
                node == b ? new[] { leaf } : Array.Empty<Descriptor>())
        };

        await source.CopyGraphAsync(destination, root, options, deadline.Token);

        Assert.True(await destination.ExistsAsync(root, deadline.Token));
        Assert.True(await destination.ExistsAsync(a, deadline.Token));
        Assert.True(await destination.ExistsAsync(b, deadline.Token));
        Assert.True(await destination.ExistsAsync(leaf, deadline.Token));
    }

    [Fact]
    public async Task CopyGraphAsync_RejectsInvalidSuccessors_BeforeStartingTheirCopiesAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var root = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("root"), MediaType.ImageConfig, deadline.Token);
        var options = new CopyGraphOptions
        {
            FindSuccessorsAsync = (_, _, _) => Task.FromResult<IEnumerable<Descriptor>>(
                new[] { new Descriptor { MediaType = MediaType.ImageConfig, Digest = "" } })
        };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => source.CopyGraphAsync(new MemoryStore(), root, options, deadline.Token));
    }

    [Theory]
    [InlineData(16, 64)]
    [InlineData(64, 128)]
    public async Task CopyGraphAsync_DoesNotRescanSharedDescendants_WhenRegisteringNewParentsAsync(
        int parentCount, int layerCount)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var destination = new MemoryStore();
        var layers = new List<Descriptor>();
        for (var i = 0; i < layerCount; i++)
        {
            layers.Add(await PushContentAsync(
                source, Encoding.UTF8.GetBytes($"layer-{i}"), MediaType.ImageLayer, deadline.Token));
        }
        var shared = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("shared"), MediaType.ImageConfig, deadline.Token);
        var options = new CopyGraphOptions
        {
            FindSuccessorsAsync = (_, node, _) => Task.FromResult<IEnumerable<Descriptor>>(
                node == shared ? layers :
                node.MediaType == MediaType.ImageConfig ? new[] { shared } : Array.Empty<Descriptor>())
        };
        var proxy = new Proxy { Source = source, Cache = new MemoryStore() };
        using var limiter = new SemaphoreSlim(options.Concurrency, options.Concurrency);
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();
        var lookups = new DependencyLookupCounter();
        var parents = new Dictionary<BasicDescriptor, List<BasicDescriptor>>(lookups);
        var nodesWithSuccessors = new HashSet<BasicDescriptor>();

        await source.CopyGraphAsync(
            destination, shared, proxy, options, limiter, copied, parents, nodesWithSuccessors, deadline.Token);
        for (var i = 0; i < parentCount; i++)
        {
            var parent = await PushContentAsync(
                source, Encoding.UTF8.GetBytes($"parent-{i}"), MediaType.ImageConfig, deadline.Token);
            await source.CopyGraphAsync(
                destination, parent, proxy, options, limiter, copied, parents, nodesWithSuccessors, deadline.Token);
            Assert.True(await destination.ExistsAsync(parent, deadline.Token));
        }

        // Count graph lookups rather than elapsed time to catch repeated scans without timing flakiness.
        Assert.True(lookups.HashCalls <= 4 * (parentCount + layerCount),
            $"Expected linear graph lookups, but observed {lookups.HashCalls} " +
            $"for {parentCount} parents and {layerCount} shared layers.");
        foreach (var layer in layers)
        {
            Assert.True(await destination.ExistsAsync(layer, deadline.Token));
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    public async Task CopyGraphAsync_DoesNotRescanAncestors_WhenDependenciesAreNewNodesAsync(int nodeCount)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var destination = new MemoryStore();
        var nodes = new List<Descriptor>();
        for (var i = 0; i < nodeCount; i++)
        {
            nodes.Add(await PushContentAsync(
                source, Encoding.UTF8.GetBytes($"node-{i}"), MediaType.ImageConfig, deadline.Token));
        }
        var successors = new Dictionary<BasicDescriptor, Descriptor>();
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            successors.Add(nodes[i].BasicDescriptor, nodes[i + 1]);
        }
        var options = new CopyGraphOptions
        {
            FindSuccessorsAsync = (_, node, _) => Task.FromResult<IEnumerable<Descriptor>>(
                successors.TryGetValue(node.BasicDescriptor, out var successor)
                    ? new[] { successor } : Array.Empty<Descriptor>())
        };
        var proxy = new Proxy { Source = source, Cache = new MemoryStore() };
        using var limiter = new SemaphoreSlim(options.Concurrency, options.Concurrency);
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();
        var lookups = new DependencyLookupCounter();
        var parents = new Dictionary<BasicDescriptor, List<BasicDescriptor>>(lookups);
        var nodesWithSuccessors = new HashSet<BasicDescriptor>();

        await source.CopyGraphAsync(
            destination, nodes[0], proxy, options, limiter, copied, parents, nodesWithSuccessors, deadline.Token);

        Assert.True(lookups.HashCalls <= 8 * nodeCount,
            $"Expected linear graph lookups, but observed {lookups.HashCalls} for a {nodeCount}-node chain.");
        foreach (var node in nodes)
        {
            Assert.True(await destination.ExistsAsync(node, deadline.Token));
        }
    }

    [Theory]
    [InlineData(64, 1)]
    [InlineData(512, 1)]
    [InlineData(512, 3)]
    public async Task CopyGraphAsync_DoesNotRescanAncestors_WhenManifestsShareALeafConfigAsync(
        int manifestCount, int concurrency)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var source = new MemoryStore();
        var destination = new MemoryStore();
        await PushContentAsync(source, Encoding.UTF8.GetBytes("{}"), MediaType.EmptyJson, deadline.Token);
        var manifests = new List<Descriptor>();
        Descriptor? subject = null;
        for (var i = 0; i < manifestCount; i++)
        {
            var manifest = new Manifest
            {
                SchemaVersion = 2,
                MediaType = MediaType.ImageManifest,
                ArtifactType = "application/vnd.example.artifact.v1",
                Config = Descriptor.Empty,
                Layers = Array.Empty<Descriptor>(),
                Subject = subject
            };
            subject = await PushContentAsync(
                source, JsonSerializer.SerializeToUtf8Bytes(manifest), MediaType.ImageManifest, deadline.Token);
            manifests.Add(subject);
        }
        var options = new CopyGraphOptions { Concurrency = concurrency };
        var proxy = new Proxy { Source = source, Cache = new MemoryStore() };
        using var limiter = new SemaphoreSlim(concurrency, concurrency);
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();
        var lookups = new DependencyLookupCounter();
        var parents = new Dictionary<BasicDescriptor, List<BasicDescriptor>>(lookups);
        var nodesWithSuccessors = new HashSet<BasicDescriptor>();

        await source.CopyGraphAsync(
            destination, manifests[^1], proxy, options, limiter, copied, parents, nodesWithSuccessors, deadline.Token);

        Assert.True(lookups.HashCalls <= 8 * (manifestCount + 1),
            $"Expected linear graph lookups, but observed {lookups.HashCalls} " +
            $"for {manifestCount} manifests sharing a leaf config.");
        Assert.True(await destination.ExistsAsync(Descriptor.Empty, deadline.Token));
        foreach (var manifest in manifests)
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

    [Fact]
    public async Task CopyGraphAsync_CancelsKnownDuplicate_WhenSharedLayerUploadIsPendingAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var graph = await CreateSharedLayerGraphAsync(deadline.Token);
        var destination = new DependencyCheckingTarget(graph.Layer);
        var options = new CopyGraphOptions { Concurrency = 1 };
        var proxy = new Proxy { Source = graph.Source, Cache = new MemoryStore() };
        using var limiter = new SemaphoreSlim(1, 1);
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();
        var parents = new Dictionary<BasicDescriptor, List<BasicDescriptor>>();
        var nodesWithSuccessors = new HashSet<BasicDescriptor>();
        var owner = graph.Source.CopyGraphAsync(
            destination, graph.Layer, proxy, options, limiter,
            copied, parents, nodesWithSuccessors, cancellation.Token);
        Task? duplicate = null;
        try
        {
            await destination.LayerUploadStarted.Task.WaitAsync(deadline.Token);
            Assert.False(owner.IsCompleted);

            // This call reaches the existing Lazy synchronously, before returning its pending Task.
            duplicate = graph.Source.CopyGraphAsync(
                destination, graph.Layer, proxy, options, limiter,
                copied, parents, nodesWithSuccessors, cancellation.Token);
            Assert.False(duplicate.IsCompleted);
            Assert.Single(copied);

            await cancellation.CancelAsync();
            var ownerError = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => owner.WaitAsync(deadline.Token));
            var duplicateError = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => duplicate.WaitAsync(deadline.Token));

            Assert.Equal(cancellation.Token, ownerError.CancellationToken);
            Assert.Equal(cancellation.Token, duplicateError.CancellationToken);
            Assert.True(owner.IsCanceled);
            Assert.True(duplicate.IsCanceled);
            Assert.False(deadline.IsCancellationRequested);
            Assert.Equal(1, destination.SharedLayerUploadCount);
        }
        finally
        {
            await cancellation.CancelAsync();
            destination.ReleaseLayerUpload();
            // Observe both tasks during cleanup even if an assertion above fails.
            await Task.WhenAll(owner, duplicate ?? Task.CompletedTask)
                .ConfigureAwait(
                    ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyGraphAsync_DrainsCanceledCallbacks_BeforeCompletingAsync(bool extendedCopy)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var source = new MemoryStore();
        var root = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("root"), MediaType.ImageConfig, deadline.Token);
        var child = await PushContentAsync(
            source, Encoding.UTF8.GetBytes("child"), MediaType.ImageConfig, deadline.Token);
        var destination = new MemoryStore();
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new ExtendedCopyGraphOptions
        {
            FindSuccessorsAsync = (_, node, _) => Task.FromResult<IEnumerable<Descriptor>>(
                node == root ? new[] { child } : Array.Empty<Descriptor>()),
            PostCopyAsync = async (node, cancellationToken) =>
            {
                if (node == child)
                {
                    callbackStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    finally
                    {
                        cleanupStarted.TrySetResult();
                        await releaseCleanup.Task;
                    }
                }
            }
        };
        var copy = extendedCopy
            ? source.ExtendedCopyGraphAsync(destination, root, options, cancellation.Token)
            : source.CopyGraphAsync(destination, root, options, cancellation.Token);
        try
        {
            await callbackStarted.Task.WaitAsync(deadline.Token);
            await cancellation.CancelAsync();
            await cleanupStarted.Task.WaitAsync(deadline.Token);
            Assert.False(copy.IsCompleted);

            releaseCleanup.TrySetResult();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => copy.WaitAsync(deadline.Token));

            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.True(copy.IsCanceled);
            Assert.False(deadline.IsCancellationRequested);
            Assert.False(await destination.ExistsAsync(root, deadline.Token));
        }
        finally
        {
            releaseCleanup.TrySetResult();
            await cancellation.CancelAsync();
            await copy.ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
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
        try
        {
            await destination.LayerUploadStarted.Task.WaitAsync(cancellationToken);
            var observationEnded = Task.Delay(TimeSpan.FromSeconds(5), observation.Token);
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

    private sealed class DependencyLookupCounter : IEqualityComparer<BasicDescriptor>
    {
        private int _hashCalls;

        public int HashCalls => Volatile.Read(ref _hashCalls);

        public bool Equals(BasicDescriptor? left, BasicDescriptor? right) => left == right;

        public int GetHashCode(BasicDescriptor value)
        {
            Interlocked.Increment(ref _hashCalls);
            return value.GetHashCode();
        }
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
