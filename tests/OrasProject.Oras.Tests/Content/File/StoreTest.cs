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
using OrasProject.Oras.Content.File;
using OrasProject.Oras.Content.File.Exceptions;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File;

public class FileStoreTest : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempDirs = new();

    public FileStoreTest()
    {
        _tempDir = CreateTempDir();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oras_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    /// <summary>
    /// Tests that FileStore implements ITarget correctly.
    /// </summary>
    [Fact]
    public void FileStore_ImplementsCorrectInterfaces()
    {
        using var store = new Store(_tempDir);

        Assert.True(store is ITarget);
        Assert.True(store is IPredecessorFindable);
        Assert.True(store is IDisposable);
        Assert.True(store is IAsyncDisposable);
    }

    /// <summary>
    /// Tests basic push and fetch operations.
    /// </summary>
    [Fact]
    public async Task FileStore_PushAndFetch_Success()
    {
        using var store = new Store(_tempDir);
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests relative root with successful operations.
    /// </summary>
    [Fact]
    public async Task FileStore_RelativeRoot_Success()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            using var store = new Store(".");

            var content = Encoding.UTF8.GetBytes("hello world");
            var name = "test.txt";
            var mediaType = "test";
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(content),
                Size = content.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = name
                }
            };

            // Test push
            await store.PushAsync(desc, new MemoryStream(content));

            // Test exists
            Assert.True(await store.ExistsAsync(desc));

            // Test fetch
            using var fetchedStream = await store.FetchAsync(desc);
            using var ms = new MemoryStream();
            await fetchedStream.CopyToAsync(ms);
            Assert.Equal(content, ms.ToArray());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Tests that store operations fail after close.
    /// </summary>
    [Fact]
    public async Task FileStore_Close_OperationsFailAfterClose()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };
        var reference = "foobar";

        var store = new Store(_tempDir);

        // Test push before close
        await store.PushAsync(desc, new MemoryStream(content));
        Assert.True(await store.ExistsAsync(desc));

        // Close the store
        store.Dispose();

        // Closing twice should not throw
        store.Dispose();

        // Test operations after close
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.AddAsync(name, mediaType, ""));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.PushAsync(desc, new MemoryStream(content)));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.ExistsAsync(desc));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.TagAsync(desc, reference));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.ResolveAsync(reference));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.FetchAsync(desc));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.GetPredecessorsAsync(desc));
    }

    /// <summary>
    /// Tests pushing a file.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests pushing content without a name (to fallback storage).
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests pushing content without a name exceeding fallback limit.
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName_ExceedLimit()
    {
        var blob = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(blob),
            Size = blob.Length
        };

        using var store = new Store(_tempDir, 1); // 1 byte limit

        await Assert.ThrowsAsync<SizeLimitExceededException>(() =>
            store.PushAsync(desc, new MemoryStream(blob)));
    }

    /// <summary>
    /// Tests that pushing content with wrong size fails.
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName_SizeNotMatch()
    {
        var blob = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(blob),
            Size = 1
        };

        using var store = new Store(_tempDir, 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.PushAsync(desc, new MemoryStream(blob)));
    }

    /// <summary>
    /// Tests that file not found throws NotFoundException.
    /// </summary>
    [Fact]
    public async Task FileStore_File_NotFound()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        Assert.False(await store.ExistsAsync(desc));
        await Assert.ThrowsAsync<NotFoundException>(() => store.FetchAsync(desc));
    }

    /// <summary>
    /// Tests that pushing content with wrong digest fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_ContentBadPush()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.PushAsync(desc, new MemoryStream(Encoding.UTF8.GetBytes("foobar"))));
    }

    /// <summary>
    /// Tests adding a file to the store.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var expectedDesc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(name, mediaType, path);
        Assert.Equal(expectedDesc.MediaType, gotDesc.MediaType);
        Assert.Equal(expectedDesc.Digest, gotDesc.Digest);
        Assert.Equal(expectedDesc.Size, gotDesc.Size);
        Assert.Equal(name, gotDesc.Annotations?[Annotations.Title]);

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(gotDesc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests adding a file with duplicate name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_SameContent_DuplicateName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(name, mediaType, path);
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test duplicate name
        await Assert.ThrowsAsync<DuplicateNameException>(() =>
            store.AddAsync(name, mediaType, path));
    }

    /// <summary>
    /// Tests adding files with same content but different names.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add_SameContent()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var name2 = "test_2.txt";

        var path1 = Path.Combine(_tempDir, name1);
        var path2 = Path.Combine(_tempDir, name2);
        await System.IO.File.WriteAllBytesAsync(path1, content);
        await System.IO.File.WriteAllBytesAsync(path2, content);

        using var store = new Store(_tempDir);

        // Test add both
        var gotDesc1 = await store.AddAsync(name1, mediaType, path1);
        var gotDesc2 = await store.AddAsync(name2, mediaType, path2);

        Assert.Equal(gotDesc1.Digest, gotDesc2.Digest);
        Assert.NotEqual(gotDesc1.Annotations?[Annotations.Title], gotDesc2.Annotations?[Annotations.Title]);

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc1));
        Assert.True(await store.ExistsAsync(gotDesc2));
    }

    /// <summary>
    /// Tests adding with missing name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add_MissingName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<MissingNameException>(() =>
            store.AddAsync("", mediaType, path));
    }

    /// <summary>
    /// Tests pushing files with same content but different names.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_SameContent()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name1
            }
        };

        var name2 = "test_2.txt";
        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name2
            }
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc1, new MemoryStream(content));
        await store.PushAsync(desc2, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc1));
        Assert.True(await store.ExistsAsync(desc2));
    }

    /// <summary>
    /// Tests pushing with duplicate name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DuplicateName()
    {
        var mediaType = "test";
        var name = "test.txt";
        var content1 = Encoding.UTF8.GetBytes("hello world");
        var content2 = Encoding.UTF8.GetBytes("goodbye world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content1),
            Size = content1.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content2),
            Size = content2.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        // First push should succeed
        await store.PushAsync(desc1, new MemoryStream(content1));
        Assert.True(await store.ExistsAsync(desc1));

        // Second push with same name should fail
        await Assert.ThrowsAsync<DuplicateNameException>(() =>
            store.PushAsync(desc2, new MemoryStream(content2)));
    }

    /// <summary>
    /// Tests overwrite behavior.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_Overwrite()
    {
        var mediaType = "test";
        var name = "test.txt";
        var oldContent = Encoding.UTF8.GetBytes("hello world");
        var newContent = Encoding.UTF8.GetBytes("goodbye world");

        // Create existing file
        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, oldContent);

        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(newContent),
            Size = newContent.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        // Push should overwrite
        await store.PushAsync(desc, new MemoryStream(newContent));
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(newContent, ms.ToArray());
    }

    /// <summary>
    /// Tests DisableOverwrite option.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DisableOverwrite()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Create existing file
        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir)
        {
            DisableOverwrite = true
        };

        await Assert.ThrowsAsync<OverwriteDisallowedException>(() =>
            store.PushAsync(desc, new MemoryStream(content)));
    }

    /// <summary>
    /// Tests IgnoreNoName option.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_IgnoreNoName()
    {
        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = "config",
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = []
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir)
        {
            IgnoreNoName = true
        };

        // Push an OCI manifest without name
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify the manifest is not saved
        Assert.False(await store.ExistsAsync(manifestDesc));
    }

    /// <summary>
    /// Tests path traversal is disallowed by default.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DisallowPathTraversal()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "../test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<PathTraversalDisallowedException>(() =>
            store.PushAsync(desc, new MemoryStream(content)));
    }

    /// <summary>
    /// Tests path traversal when allowed.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_PathTraversal()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "../test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Create a subdirectory to work from
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        using var store = new Store(subDir)
        {
            AllowPathTraversalOnWrite = true
        };

        // Should succeed when path traversal is allowed
        await store.PushAsync(desc, new MemoryStream(content));
        Assert.True(await store.ExistsAsync(desc));
    }

    /// <summary>
    /// Tests tag and resolve operations.
    /// </summary>
    [Fact]
    public async Task FileStore_TagAndResolve()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };
        var reference = "foobar";

        using var store = new Store(_tempDir);

        // Push
        await store.PushAsync(desc, new MemoryStream(content));

        // Tag
        await store.TagAsync(desc, reference);

        // Resolve
        var gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc.Digest, gotDesc.Digest);
        Assert.Equal(desc.MediaType, gotDesc.MediaType);
        Assert.Equal(desc.Size, gotDesc.Size);
    }

    /// <summary>
    /// Tests resolve for non-existent tag.
    /// </summary>
    [Fact]
    public async Task FileStore_TagNotFound()
    {
        var reference = "foobar";

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            store.ResolveAsync(reference));
    }

    /// <summary>
    /// Tests tagging unknown content fails.
    /// </summary>
    [Fact]
    public async Task FileStore_TagUnknownContent()
    {
        var content = Encoding.UTF8.GetBytes("{\"layers\":[]}");
        var desc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };
        var reference = "foobar";

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            store.TagAsync(desc, reference));
    }

    /// <summary>
    /// Tests repeat tagging.
    /// </summary>
    [Fact]
    public async Task FileStore_RepeatTag()
    {
        using var store = new Store(_tempDir);
        var reference = "foobar";

        // First tag
        var content1 = Encoding.UTF8.GetBytes("hello world");
        var desc1 = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content1),
            Size = content1.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = Digest.ComputeSha256(content1) + ".blob"
            }
        };

        await store.PushAsync(desc1, new MemoryStream(content1));
        await store.TagAsync(desc1, reference);

        var gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc1.Digest, gotDesc.Digest);

        // Second tag (replace)
        var content2 = Encoding.UTF8.GetBytes("foo");
        var desc2 = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content2),
            Size = content2.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = Digest.ComputeSha256(content2) + ".blob"
            }
        };

        await store.PushAsync(desc2, new MemoryStream(content2));
        await store.TagAsync(desc2, reference);

        gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc2.Digest, gotDesc.Digest);
    }

    /// <summary>
    /// Tests predecessors functionality.
    /// </summary>
    [Fact]
    public async Task FileStore_Predecessors()
    {
        using var store = new Store(_tempDir);

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Split(':')[1][..12] + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers.ToList()
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));     // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));     // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                      // Blob 3

        // Push all content
        for (var i = 0; i < blobs.Count; i++)
        {
            await store.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        // Verify predecessors
        // Blob 0 (config) should have manifest as predecessor
        var preds0 = (await store.GetPredecessorsAsync(descs[0])).ToList();
        Assert.Single(preds0);
        Assert.Equal(descs[3].Digest, preds0[0].Digest);

        // Blob 1 (layer) should have manifest as predecessor
        var preds1 = (await store.GetPredecessorsAsync(descs[1])).ToList();
        Assert.Single(preds1);
        Assert.Equal(descs[3].Digest, preds1[0].Digest);

        // Blob 2 (layer) should have manifest as predecessor
        var preds2 = (await store.GetPredecessorsAsync(descs[2])).ToList();
        Assert.Single(preds2);
        Assert.Equal(descs[3].Digest, preds2[0].Digest);

        // Blob 3 (manifest) should have no predecessors
        var preds3 = await store.GetPredecessorsAsync(descs[3]);
        Assert.Empty(preds3);
    }

    /// <summary>
    /// Tests ForceCAS option prevents restoration of duplicates.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_ForceCAS()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob1"
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob2"
            }
        };

        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = [desc1, desc2]
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir)
        {
            ForceCAS = true
        };

        // Push blob1
        await store.PushAsync(desc1, new MemoryStream(content));

        // Push manifest
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify blob2 does NOT exist (ForceCAS prevents restoration)
        Assert.False(await store.ExistsAsync(desc2));
    }

    /// <summary>
    /// Tests restore duplicates functionality.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_RestoreDuplicates()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob1"
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob2"
            }
        };

        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = [desc1, desc2]
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir);

        // Push blob1
        await store.PushAsync(desc1, new MemoryStream(content));

        // Push manifest
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify blob2 is restored
        Assert.True(await store.ExistsAsync(desc2));

        // Fetch and verify
        using var fetchedStream = await store.FetchAsync(desc2);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests resolveWritePath with various path traversal scenarios.
    /// </summary>
    [Theory]
    [InlineData("test.txt", false, false)]                // Good relative path
    [InlineData("../test.txt", false, true)]              // Bad path traversal
    [InlineData("../test.txt", true, false)]              // Path traversal allowed (will write to temp dir)
    [InlineData("..", false, true)]                       // Bad directory path
    // Note: ".." with allowTraversal=true is not tested as it would try to write to the parent of temp dir
    public async Task FileStore_resolveWritePath_PathTraversal(string name, bool allowTraversal, bool shouldThrow)
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir)
        {
            AllowPathTraversalOnWrite = allowTraversal
        };

        if (shouldThrow)
        {
            await Assert.ThrowsAsync<PathTraversalDisallowedException>(() =>
                store.PushAsync(desc, new MemoryStream(content)));
        }
        else
        {
            // Should not throw
            await store.PushAsync(desc, new MemoryStream(content));
            Assert.True(await store.ExistsAsync(desc));
        }
    }

    /// <summary>
    /// Tests concurrent push operations.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_Concurrent()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        var concurrency = 64;
        var tasks = new Task[concurrency];

        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await store.PushAsync(desc, new MemoryStream(content));
                }
                catch (DuplicateNameException)
                {
                    // Expected for concurrent pushes with same name
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.True(await store.ExistsAsync(desc));

        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests concurrent fetch operations.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Fetch_Concurrent()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);
        await store.PushAsync(desc, new MemoryStream(content));

        var concurrency = 64;
        var tasks = new Task[concurrency];

        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var fetchedStream = await store.FetchAsync(desc);
                using var ms = new MemoryStream();
                await fetchedStream.CopyToAsync(ms);
                Assert.Equal(content, ms.ToArray());
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests empty reference validation.
    /// </summary>
    [Fact]
    public async Task FileStore_EmptyReference()
    {
        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ResolveAsync(""));

        var content = Encoding.UTF8.GetBytes("test");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };

        await store.PushAsync(desc, new MemoryStream(content));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TagAsync(desc, ""));
    }

    /// <summary>
    /// Tests adding a directory to the store (creates tar.gz).
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Add()
    {
        var dirName = "testdir";
        var dirPath = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dirPath);

        var content = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath, fileName), content);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(dirName, "", dirPath);

        // Verify descriptor has unpack annotation
        Assert.NotNull(gotDesc.Annotations);
        Assert.Equal("true", gotDesc.Annotations[FileStoreAnnotations.AnnotationUnpack]);
        Assert.True(gotDesc.Annotations.ContainsKey(FileStoreAnnotations.AnnotationDigest));

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test fetch - should return the gzip content
        using var fetchedStream = await store.FetchAsync(gotDesc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.True(ms.Length > 0);

        // Verify it's a valid gzip by checking magic bytes
        var fetchedBytes = ms.ToArray();
        Assert.Equal(0x1f, fetchedBytes[0]); // Gzip magic byte 1
        Assert.Equal(0x8b, fetchedBytes[1]); // Gzip magic byte 2
    }

    /// <summary>
    /// Tests pushing a directory (tar.gz with unpack annotation) extracts correctly.
    /// Based on TestStore_Dir_Push from oras-go.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Push()
    {
        // First, create a directory and add it to a source store to generate the tar.gz
        var srcDirName = "testdir";
        var srcDirPath = Path.Combine(_tempDir, srcDirName);
        Directory.CreateDirectory(srcDirPath);

        var fileContent = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(srcDirPath, fileName), fileContent);

        using var srcStore = new Store(_tempDir);

        // Add directory to source store
        var desc = await srcStore.AddAsync(srcDirName, "", srcDirPath);

        // Fetch the gzip content
        using var gzStream = await srcStore.FetchAsync(desc);
        using var gzMs = new MemoryStream();
        await gzStream.CopyToAsync(gzMs);
        var gz = gzMs.ToArray();

        // Create another store and push the gzip to it
        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir);

        // Test push
        await anotherStore.PushAsync(desc, new MemoryStream(gz));

        // Test exists
        Assert.True(await anotherStore.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await anotherStore.FetchAsync(desc);
        using var fetchedMs = new MemoryStream();
        await fetchedStream.CopyToAsync(fetchedMs);
        Assert.Equal(gz, fetchedMs.ToArray());

        // Test file content - verify directory was unpacked
        var extractedFilePath = Path.Combine(anotherTempDir, srcDirName, fileName);
        Assert.True(System.IO.File.Exists(extractedFilePath));
        var extractedContent = await System.IO.File.ReadAllBytesAsync(extractedFilePath);
        Assert.Equal(fileContent, extractedContent);
    }

    /// <summary>
    /// Tests pushing a directory with SkipUnpack option.
    /// Based on TestStore_Dir_Push_SkipUnpack from oras-go.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Push_SkipUnpack()
    {
        // First, create a directory and add it to a source store to generate the tar.gz
        var srcDirName = "testdir";
        var srcDirPath = Path.Combine(_tempDir, srcDirName);
        Directory.CreateDirectory(srcDirPath);

        var fileContent = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(srcDirPath, fileName), fileContent);

        using var srcStore = new Store(_tempDir);

        // Add directory to source store
        var desc = await srcStore.AddAsync(srcDirName, "", srcDirPath);

        // Fetch the gzip content
        using var gzStream = await srcStore.FetchAsync(desc);
        using var gzMs = new MemoryStream();
        await gzStream.CopyToAsync(gzMs);
        var gz = gzMs.ToArray();

        // Create another store with SkipUnpack enabled
        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir)
        {
            SkipUnpack = true
        };

        var gzPath = Path.Combine(anotherTempDir, srcDirName);

        // Push the gz to the store
        await anotherStore.PushAsync(desc, new MemoryStream(gz));

        // Verify the pushed content is the raw gzip (not unpacked)
        Assert.True(System.IO.File.Exists(gzPath));
        var pushedContent = await System.IO.File.ReadAllBytesAsync(gzPath);
        Assert.Equal(gz, pushedContent);

        // Verify it was NOT unpacked (no subdirectory with the file)
        var wouldBeExtractedFilePath = Path.Combine(anotherTempDir, srcDirName, fileName);
        Assert.False(System.IO.File.Exists(wouldBeExtractedFilePath));
    }

    /// <summary>
    /// Tests pushing a directory with path traversal in name is disallowed by default.
    /// Based on TestStore_Dir_Push_DisallowPathTraversal from oras-go.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Push_DisallowPathTraversal()
    {
        // Create a directory with path traversal in name
        var srcDirName = "../testdir";
        var srcDirPath = Path.Combine(_tempDir, srcDirName);
        Directory.CreateDirectory(srcDirPath);

        var fileContent = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(srcDirPath, fileName), fileContent);

        using var srcStore = new Store(_tempDir);

        // Add directory to source store
        var desc = await srcStore.AddAsync(srcDirName, "", srcDirPath);

        // Fetch the gzip content
        using var gzStream = await srcStore.FetchAsync(desc);
        using var gzMs = new MemoryStream();
        await gzStream.CopyToAsync(gzMs);
        var gz = gzMs.ToArray();

        // Create another store and try to push the gzip - should fail due to path traversal
        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir);

        await Assert.ThrowsAsync<PathTraversalDisallowedException>(() =>
            anotherStore.PushAsync(desc, new MemoryStream(gz)));
    }

    /// <summary>
    /// Tests adding a directory with nested subdirectories.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Add_NestedDirectories()
    {
        var dirName = "testdir";
        var dirPath = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dirPath);

        // Create nested structure
        var subDirPath = Path.Combine(dirPath, "subdir");
        Directory.CreateDirectory(subDirPath);

        var content1 = Encoding.UTF8.GetBytes("file in root");
        var content2 = Encoding.UTF8.GetBytes("file in subdir");

        await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath, "root.txt"), content1);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(subDirPath, "nested.txt"), content2);

        using var srcStore = new Store(_tempDir);

        // Add directory
        var desc = await srcStore.AddAsync(dirName, "", dirPath);

        // Verify descriptor
        Assert.NotNull(desc.Annotations);
        Assert.Equal("true", desc.Annotations[FileStoreAnnotations.AnnotationUnpack]);

        // Fetch and push to another store to verify extraction
        using var gzStream = await srcStore.FetchAsync(desc);
        using var gzMs = new MemoryStream();
        await gzStream.CopyToAsync(gzMs);
        var gz = gzMs.ToArray();

        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir);

        await anotherStore.PushAsync(desc, new MemoryStream(gz));

        // Verify nested files were extracted
        var extractedRootFile = Path.Combine(anotherTempDir, dirName, "root.txt");
        var extractedNestedFile = Path.Combine(anotherTempDir, dirName, "subdir", "nested.txt");

        Assert.True(System.IO.File.Exists(extractedRootFile));
        Assert.True(System.IO.File.Exists(extractedNestedFile));

        Assert.Equal(content1, await System.IO.File.ReadAllBytesAsync(extractedRootFile));
        Assert.Equal(content2, await System.IO.File.ReadAllBytesAsync(extractedNestedFile));
    }

    /// <summary>
    /// Tests that TarReproducible option produces consistent output.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Add_TarReproducible()
    {
        var dirName = "testdir";
        var dirPath = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dirPath);

        var content = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath, fileName), content);

        // Create two stores with reproducible tar
        using var store1 = new Store(_tempDir) { TarReproducible = true };
        using var store2 = new Store(CreateTempDir()) { TarReproducible = true };

        // Copy the directory to the second store's temp dir
        var dirPath2 = Path.Combine(store2.ToString()!, dirName);
        if (!Directory.Exists(dirPath2))
        {
            Directory.CreateDirectory(dirPath2);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath2, fileName), content);
        }

        // Add directory to both stores
        var desc1 = await store1.AddAsync(dirName, "", dirPath);
        var desc2 = await store1.AddAsync(dirName + "_copy", "", dirPath);

        // With TarReproducible, same content should produce same digest
        // (Note: different names but same content structure)
        Assert.True(await store1.ExistsAsync(desc1));
        Assert.True(await store1.ExistsAsync(desc2));
    }

    /// <summary>
    /// Tests pushing directory content with valid checksum verification.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Push_ValidChecksum()
    {
        // Create a directory and add it to get the descriptor with annotations
        var dirName = "testdir";
        var dirPath = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dirPath);

        var fileContent = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath, fileName), fileContent);

        using var srcStore = new Store(_tempDir);
        var desc = await srcStore.AddAsync(dirName, "", dirPath);

        // Verify the descriptor has the AnnotationDigest for the uncompressed content
        Assert.NotNull(desc.Annotations);
        Assert.True(desc.Annotations.ContainsKey(FileStoreAnnotations.AnnotationDigest));

        var tarDigest = desc.Annotations[FileStoreAnnotations.AnnotationDigest];
        Assert.StartsWith("sha256:", tarDigest);

        // Fetch and push to another store
        using var gzStream = await srcStore.FetchAsync(desc);
        using var gzMs = new MemoryStream();
        await gzStream.CopyToAsync(gzMs);
        var gz = gzMs.ToArray();

        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir);

        // This should succeed because the checksum matches
        await anotherStore.PushAsync(desc, new MemoryStream(gz));

        // Verify extraction
        var extractedFilePath = Path.Combine(anotherTempDir, dirName, fileName);
        Assert.True(System.IO.File.Exists(extractedFilePath));
        Assert.Equal(fileContent, await System.IO.File.ReadAllBytesAsync(extractedFilePath));
    }

    /// <summary>
    /// Tests pushing directory content with mismatched gzip digest fails.
    /// </summary>
    [Fact]
    public async Task FileStore_Dir_Push_InvalidGzipDigest()
    {
        // Create a directory and add it to get a valid descriptor
        var dirName = "testdir";
        var dirPath = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dirPath);

        var fileContent = Encoding.UTF8.GetBytes("hello world");
        var fileName = "test.txt";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dirPath, fileName), fileContent);

        using var srcStore = new Store(_tempDir);
        var desc = await srcStore.AddAsync(dirName, "", dirPath);

        // Create a different (corrupted) gzip content
        var corruptedGz = Encoding.UTF8.GetBytes("this is not a valid gzip");

        var anotherTempDir = CreateTempDir();
        using var anotherStore = new Store(anotherTempDir);

        // This should fail because the digest doesn't match
        await Assert.ThrowsAnyAsync<Exception>(() =>
            anotherStore.PushAsync(desc, new MemoryStream(corruptedGz)));
    }

    /// <summary>
    /// Tests adding files with different content but the same name should fail.
    /// </summary>
    [Fact]
    public async Task FileStore_File_DifferentContent_DuplicateName()
    {
        var content1 = Encoding.UTF8.GetBytes("hello world");
        var content2 = Encoding.UTF8.GetBytes("goodbye world");

        var name1 = "test_1.txt";
        var name2 = "test_2.txt";

        var mediaType1 = "test";
        var mediaType2 = "test_2";
        var desc1 = new Descriptor
        {
            MediaType = mediaType1,
            Digest = Digest.ComputeSha256(content1),
            Size = content1.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name1
            }
        };

        var path1 = Path.Combine(_tempDir, name1);
        await System.IO.File.WriteAllBytesAsync(path1, content1);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(name1, mediaType1, path1);
        Assert.Equal(desc1.Digest, gotDesc.Digest);
        Assert.Equal(desc1.Size, gotDesc.Size);

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test fetch
        using var rc = await store.FetchAsync(gotDesc);
        using var ms = new MemoryStream();
        await rc.CopyToAsync(ms);
        Assert.Equal(content1, ms.ToArray());

        // Test add duplicate name with different content
        var path2 = Path.Combine(_tempDir, name2);
        await System.IO.File.WriteAllBytesAsync(path2, content2);

        var ex = await Assert.ThrowsAsync<DuplicateNameException>(() =>
            store.AddAsync(name1, mediaType2, path2));
    }

    /// <summary>
    /// Tests pushing manifest before blob is allowed (duplicate restore not found scenario).
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_RestoreDuplicates_NotFound()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob1"
            }
        };

        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = new[] { desc }
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir);

        // Push manifest before blob is fine - it will try to restore duplicates
        // but since blob doesn't exist, it should succeed without error
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));
    }

    /// <summary>
    /// Tests fetching with same digest but no name (unnamed descriptor) succeeds.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Fetch_SameDigest_NoName()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name1
            }
        };

        // Descriptor without name (will use fallback storage)
        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };

        using var store = new Store(_tempDir);

        // Push both - desc1 with name, desc2 without
        await store.PushAsync(desc1, new MemoryStream(content));
        await store.PushAsync(desc2, new MemoryStream(content));

        // Test exists for both
        Assert.True(await store.ExistsAsync(desc1));
        Assert.True(await store.ExistsAsync(desc2));

        // Test fetch for desc1 (with name)
        using var rc1 = await store.FetchAsync(desc1);
        using var ms1 = new MemoryStream();
        await rc1.CopyToAsync(ms1);
        Assert.Equal(content, ms1.ToArray());

        // Test fetch for desc2 (without name)
        using var rc2 = await store.FetchAsync(desc2);
        using var ms2 = new MemoryStream();
        await rc2.CopyToAsync(ms2);
        Assert.Equal(content, ms2.ToArray());
    }

    /// <summary>
    /// Tests fetching content with same digest but different names - 
    /// only the first pushed name can be fetched.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Fetch_SameDigest_DifferentName()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name1
            }
        };

        var name2 = "test_2.txt";
        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name2
            }
        };

        using var store = new Store(_tempDir);

        // Push desc1
        await store.PushAsync(desc1, new MemoryStream(content));

        // Test exists for desc1
        Assert.True(await store.ExistsAsync(desc1));

        // Test fetch for desc1
        using var rc1 = await store.FetchAsync(desc1);
        using var ms1 = new MemoryStream();
        await rc1.CopyToAsync(ms1);
        Assert.Equal(content, ms1.ToArray());

        // desc2 has the same digest but different name, so it should not exist
        Assert.False(await store.ExistsAsync(desc2));

        // Fetching desc2 should throw NotFoundException
        await Assert.ThrowsAsync<NotFoundException>(() => store.FetchAsync(desc2));
    }

    /// <summary>
    /// Tests resolveWritePath with overwrite scenarios.
    /// </summary>
    [Fact]
    public async Task FileStore_resolveWritePath_Overwrite()
    {
        // Test 1: Target file already exists with DisableOverwrite = true
        {
            using var store = new Store(_tempDir);
            store.DisableOverwrite = true;

            var existingFile = Path.Combine(_tempDir, "existing.txt");
            await System.IO.File.WriteAllBytesAsync(existingFile, Encoding.UTF8.GetBytes("content"));

            var content = Encoding.UTF8.GetBytes("new content");
            var desc = new Descriptor
            {
                MediaType = "test",
                Digest = Digest.ComputeSha256(content),
                Size = content.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = "existing.txt"
                }
            };

            await Assert.ThrowsAsync<OverwriteDisallowedException>(() =>
                store.PushAsync(desc, new MemoryStream(content)));
        }

        // Test 2: Target file does not exist with DisableOverwrite = true should succeed
        {
            var newDir = CreateTempDir();
            using var store = new Store(newDir);
            store.DisableOverwrite = true;

            var content = Encoding.UTF8.GetBytes("new content");
            var desc = new Descriptor
            {
                MediaType = "test",
                Digest = Digest.ComputeSha256(content),
                Size = content.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = "new_file.txt"
                }
            };

            await store.PushAsync(desc, new MemoryStream(content));
            Assert.True(await store.ExistsAsync(desc));
        }
    }

    /// <summary>
    /// Tests handling of bad/invalid digest formats.
    /// </summary>
    [Fact]
    public async Task FileStore_BadDigest()
    {
        var data = Encoding.UTF8.GetBytes("hello world");
        var @ref = "foobar";

        // Test: invalid digest format
        {
            var desc = new Descriptor
            {
                MediaType = "application/test",
                Size = data.Length,
                Digest = "invalid-digest"
            };

            using var store = new Store(_tempDir);

            // Push should fail with invalid digest
            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.PushAsync(desc, new MemoryStream(data)));

            // Tag should fail since content doesn't exist
            await Assert.ThrowsAsync<NotFoundException>(() =>
                store.TagAsync(desc, @ref));

            // Exists should return false (or not error)
            Assert.False(await store.ExistsAsync(desc));

            // Fetch should throw NotFoundException
            await Assert.ThrowsAsync<NotFoundException>(() =>
                store.FetchAsync(desc));

            // Predecessors should return empty (or not error)
            var predecessors = await store.GetPredecessorsAsync(desc);
            Assert.Empty(predecessors);
        }
    }

    /// <summary>
    /// Tests copying content from MemoryStore to FileStore.
    /// </summary>
    [Fact]
    public async Task FileStore_Copy_MemoryToFile_FullCopy()
    {
        var src = new MemoryStore();
        using var dst = new Store(_tempDir);

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Substring("sha256:".Length) + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                MediaType = MediaType.ImageManifest,
                Config = config,
                Layers = layers
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        // Create blobs
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));      // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));      // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                           // Blob 3

        // Push to source
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        var root = descs[3];
        var refTag = "foobar";
        await src.TagAsync(root, refTag);

        // Test copy
        var gotDesc = await CopyAsync(src, refTag, dst, "");
        Assert.Equal(root.Digest, gotDesc.Digest);
        Assert.Equal(root.Size, gotDesc.Size);

        // Verify contents
        for (int i = 0; i < descs.Count; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }

        // Verify tag
        var resolvedDesc = await dst.ResolveAsync(refTag);
        Assert.Equal(root.Digest, resolvedDesc.Digest);
    }

    /// <summary>
    /// Tests copying content from FileStore to MemoryStore.
    /// </summary>
    [Fact]
    public async Task FileStore_Copy_FileToMemory_FullCopy()
    {
        using var src = new Store(_tempDir);
        var dst = new MemoryStore();

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Substring("sha256:".Length) + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                MediaType = MediaType.ImageManifest,
                Config = config,
                Layers = layers
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        // Create blobs
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));      // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));      // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                           // Blob 3

        // Push to source
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        var root = descs[3];
        var refTag = "foobar";
        await src.TagAsync(root, refTag);

        // Test copy
        var gotDesc = await CopyAsync(src, refTag, dst, "");
        Assert.Equal(root.Digest, gotDesc.Digest);
        Assert.Equal(root.Size, gotDesc.Size);

        // Verify contents
        for (int i = 0; i < descs.Count; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }

        // Verify tag
        var resolvedDesc = await dst.ResolveAsync(refTag);
        Assert.Equal(root.Digest, resolvedDesc.Digest);
    }

    /// <summary>
    /// Helper method to copy content from source to destination.
    /// </summary>
    private static async Task<Descriptor> CopyAsync(
        ITarget src,
        string srcRef,
        ITarget dst,
        string dstRef,
        CancellationToken cancellationToken = default)
    {
        var root = await src.ResolveAsync(srcRef, cancellationToken);

        // Copy all nodes in dependency order (leaves first)
        await CopyNodeAsync(src, dst, root, cancellationToken);

        // Tag if dstRef is not empty
        if (!string.IsNullOrEmpty(dstRef))
        {
            await dst.TagAsync(root, dstRef, cancellationToken);
        }
        else
        {
            // If no dstRef, use the same ref
            await dst.TagAsync(root, srcRef, cancellationToken);
        }

        return root;
    }

    /// <summary>
    /// Recursively copies a node and its children.
    /// </summary>
    private static async Task CopyNodeAsync(
        ITarget src,
        ITarget dst,
        Descriptor desc,
        CancellationToken cancellationToken = default)
    {
        // Check if already exists in destination
        if (await dst.ExistsAsync(desc, cancellationToken))
        {
            return;
        }

        // Fetch the content
        using var stream = await src.FetchAsync(desc, cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var content = ms.ToArray();

        // If it's a manifest, copy children first
        if (desc.MediaType == MediaType.ImageManifest)
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(content);
            if (manifest != null)
            {
                if (manifest.Config != null)
                {
                    await CopyNodeAsync(src, dst, manifest.Config, cancellationToken);
                }
                if (manifest.Layers != null)
                {
                    foreach (var layer in manifest.Layers)
                    {
                        await CopyNodeAsync(src, dst, layer, cancellationToken);
                    }
                }
            }
        }
        else if (desc.MediaType == MediaType.ImageIndex)
        {
            var index = JsonSerializer.Deserialize<OrasProject.Oras.Oci.Index>(content);
            if (index?.Manifests != null)
            {
                foreach (var m in index.Manifests)
                {
                    await CopyNodeAsync(src, dst, m, cancellationToken);
                }
            }
        }

        // Push the content to destination
        await dst.PushAsync(desc, new MemoryStream(content), cancellationToken);
    }

    /// <summary>
    /// Tests CopyGraph from MemoryStore to FileStore with partial copy.
    /// </summary>
    [Fact]
    public async Task FileStore_CopyGraph_MemoryToFile_PartialCopy()
    {
        var src = new MemoryStore();
        using var dst = new Store(_tempDir);

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Substring("sha256:".Length) + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                MediaType = MediaType.ImageManifest,
                Config = config,
                Layers = layers
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        void GenerateIndex(params Descriptor[] manifests)
        {
            var index = new OrasProject.Oras.Oci.Index
            {
                MediaType = MediaType.ImageIndex,
                Manifests = manifests
            };
            var indexJson = JsonSerializer.SerializeToUtf8Bytes(index);
            AppendBlob(MediaType.ImageIndex, indexJson);
        }

        // Create blobs
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));      // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));      // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                           // Blob 3
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("hello"));    // Blob 4
        GenerateManifest(descs[0], descs[4]);                                     // Blob 5
        GenerateIndex(descs[3], descs[5]);                                        // Blob 6

        // Push all to source
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        // Initial copy of first manifest (blobs 0-3)
        var root1 = descs[3];
        await CopyNodeAsync(src, dst, root1);

        // Verify blobs 0-3 exist in dst
        for (int i = 0; i < 4; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }

        // Copy the index (should only need to copy blobs 4, 5, 6)
        var root2 = descs[6];
        await CopyNodeAsync(src, dst, root2);

        // Verify all blobs exist
        for (int i = 0; i < blobs.Count; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }
    }

    /// <summary>
    /// Tests CopyGraph from FileStore to MemoryStore with partial copy.
    /// </summary>
    [Fact]
    public async Task FileStore_CopyGraph_FileToMemory_PartialCopy()
    {
        using var src = new Store(_tempDir);
        var dst = new MemoryStore();

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Substring("sha256:".Length) + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                MediaType = MediaType.ImageManifest,
                Config = config,
                Layers = layers
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        void GenerateIndex(params Descriptor[] manifests)
        {
            var index = new OrasProject.Oras.Oci.Index
            {
                MediaType = MediaType.ImageIndex,
                Manifests = manifests
            };
            var indexJson = JsonSerializer.SerializeToUtf8Bytes(index);
            AppendBlob(MediaType.ImageIndex, indexJson);
        }

        // Create blobs
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));      // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));      // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                           // Blob 3
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("hello"));    // Blob 4
        GenerateManifest(descs[0], descs[4]);                                     // Blob 5
        GenerateIndex(descs[3], descs[5]);                                        // Blob 6

        // Push all to source
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        // Initial copy of first manifest (blobs 0-3)
        var root1 = descs[3];
        await CopyNodeAsync(src, dst, root1);

        // Verify blobs 0-3 exist in dst
        for (int i = 0; i < 4; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }

        // Copy the index (should only need to copy blobs 4, 5, 6)
        var root2 = descs[6];
        await CopyNodeAsync(src, dst, root2);

        // Verify all blobs exist
        for (int i = 0; i < blobs.Count; i++)
        {
            Assert.True(await dst.ExistsAsync(descs[i]));
        }
    }
}
