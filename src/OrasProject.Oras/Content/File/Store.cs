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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Content.File;

/// <summary>
/// Store represents a file system based store, which implements <see cref="ITarget"/>.
/// </summary>
/// <remarks>
/// <para>
/// In the file store, the contents described by names are location-addressed
/// by file paths. Meanwhile, the file paths are mapped to a virtual CAS
/// where all metadata are stored in the memory.
/// </para>
/// <para>
/// The contents that are not described by names are stored in a fallback storage,
/// which is a limited memory CAS by default.
/// As all the metadata are stored in the memory, the file store
/// cannot be restored from the file system.
/// </para>
/// <para>
/// After use, the file store needs to be closed by calling the <see cref="Close"/> method.
/// The file store cannot be used after being closed.
/// </para>
/// </remarks>
public class Store : IDisposable
{
    // _defaultFallbackPushSizeLimit specifies the default size limit for pushing no-name contents.
    const long _defaultFallbackPushSizeLimit = 1 << 22; // 4 MiB

    /// <summary>
    /// NameStatus contains a flag indicating if a name exists, and a SemaphoreSlim to serialize access per name.
    /// </summary>
    private class NameStatus : IDisposable
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public bool Exists { get; set; }

        public void Dispose()
        {
            Semaphore.Dispose();
        }
    }

    private readonly string _workingDir; // the working directory of the file store
    private int _closed; // if the store is closed - 0: false, 1: true.
    private readonly ConcurrentDictionary<string, bool> _tmpFiles = new();
    private readonly ConcurrentDictionary<string, string> _digestToPath = new();
    private readonly ConcurrentDictionary<string, NameStatus> _nameToStatus = new();

    private readonly IStorage _fallbackStorage;
    private readonly ITagStore _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="Store"/> class, using a default limited memory CAS
    /// as the fallback storage for contents without names.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <remarks>
    /// When pushing content without names, the size of content being pushed
    /// cannot exceed the default size limit: 4 MiB.
    /// </remarks>
    public Store(string workingDir)
        : this(workingDir, _defaultFallbackPushSizeLimit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Store"/> class, using a default
    /// limited memory CAS as the fallback storage for contents without names.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <param name="limit">The maximum size (in bytes) for pushed content.</param>
    /// <remarks>
    /// When pushing content without names, the size of content being pushed
    /// cannot exceed the size limit specified by the <paramref name="limit"/> parameter.
    /// </remarks>
    public Store(string workingDir, long limit)
        : this(workingDir, new LimitedStorage(new MemoryStorage(), limit))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Store"/> class,
    /// using the provided fallback storage for contents without names.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <param name="fallbackStorage">The fallback storage for contents without names.</param>
    public Store(string workingDir, IStorage fallbackStorage)
    {
        var workingDirAbs = Path.GetFullPath(workingDir);

        _workingDir = workingDirAbs;
        _fallbackStorage = fallbackStorage;
        _resolver = new MemoryTagStore();
    }

    /// <summary>
    /// Close closes the file store and cleans up all the temporary files used by it.
    /// The store cannot be used after being closed.
    /// This function is not thread-safe.
    /// </summary>
    /// <exception cref="AggregateException">Thrown when one or more temporary files cannot be deleted.</exception>
    public void Close()
    {
        if (IsClosedSet())
        {
            return;
        }
        SetClosed();

        var errors = new List<Exception>();
        foreach (var kvp in _tmpFiles)
        {
            try
            {
                System.IO.File.Delete(kvp.Key);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        // Dispose all semaphores
        foreach (var kvp in _nameToStatus)
        {
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Store"/>.
    /// </summary>
    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns true if the `closed` flag is set, otherwise returns false.
    /// </summary>
    private bool IsClosedSet()
    {
        return Volatile.Read(ref _closed) == 1;
    }

    /// <summary>
    /// Sets the `closed` flag.
    /// </summary>
    private void SetClosed()
    {
        Volatile.Write(ref _closed, 1);
    }

    /// <summary>
    /// Adds a file into the file store. Directory is not yet supported.
    /// </summary>
    /// <param name="name">The name of the file to add to the store.</param>
    /// <param name="mediaType">The media type for the content.</param>
    /// <param name="path">The file system path to the file to add.</param>
    /// <param name="cancellationToken">A cancellation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the descriptor for the added content.</returns>
    /// <exception cref="StoreClosedException">Thrown when the store has been closed.</exception>
    /// <exception cref="MissingNameException">Thrown when the name is empty or null.</exception>
    /// <exception cref="DuplicateNameException">Thrown when the name already exists in the store.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified path does not exist.</exception>
    public async Task<Descriptor> AddAsync(string name, string mediaType, string path, CancellationToken cancellationToken = default)
    {
        if (IsClosedSet())
        {
            throw new StoreClosedException();
        }
        if (string.IsNullOrEmpty(name))
        {
            throw new MissingNameException();
        }

        // check the status of the name
        var status = _nameToStatus.GetOrAdd(name, _ => new NameStatus());

        // Serialize access per name to prevent duplicate expensive I/O
        await status.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check if name already exists
            if (status.Exists)
            {
                throw new DuplicateNameException($"{name}: duplicate name");
            }

            if (string.IsNullOrEmpty(path))
            {
                path = name;
            }

            path = GetAbsolutePath(path);
            // directory path is not yet supported.
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"failed to get the file: {path}", path);
            }

            // Generate descriptor (file I/O happens here)
            var descriptor = await GenerateDescriptorFromFileAsync(fileInfo, mediaType, path, cancellationToken).ConfigureAwait(false);

            // Commit the name and annotations
            descriptor.Annotations ??= new Dictionary<string, string>();
            descriptor.Annotations[Descriptor.AnnotationTitle] = name;

            status.Exists = true;

            return descriptor;
        }
        finally
        {
            status.Semaphore.Release();
        }
    }

    /// <summary>
    /// Exists returns true if the described content exists.
    /// </summary>
    /// <param name="target">The descriptor of the content to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the content exists; otherwise, false.</returns>
    /// <exception cref="StoreClosedException">Thrown when the store has been closed.</exception>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        if (IsClosedSet())
        {
            throw new StoreClosedException();
        }

        // if the target has name, check if the name exists.
        if (target.Annotations != null && target.Annotations.TryGetValue(Descriptor.AnnotationTitle, out var name))
        {
            if (!string.IsNullOrEmpty(name) && !NameExists(name))
            {
                return false;
            }
        }

        // check if the content exists in the store
        if (_digestToPath.ContainsKey(target.Digest))
        {
            return true;
        }

        // if the content does not exist in the store,
        // then fall back to the fallback storage.
        return await _fallbackStorage.ExistsAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a reference to a descriptor.
    /// </summary>
    /// <param name="reference">The reference string to resolve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the resolved descriptor.</returns>
    /// <exception cref="StoreClosedException">Thrown when the store has been closed.</exception>
    /// <exception cref="MissingReferenceException">Thrown when the reference is empty or null.</exception>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (IsClosedSet())
        {
            throw new StoreClosedException();
        }

        if (string.IsNullOrEmpty(reference))
        {
            throw new MissingReferenceException();
        }

        return await _resolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tags a descriptor with a reference string.
    /// </summary>
    /// <param name="descriptor">The descriptor of the manifest to tag.</param>
    /// <param name="reference">The reference tag string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="StoreClosedException">Thrown when the store has been closed.</exception>
    /// <exception cref="MissingReferenceException">Thrown when the reference is empty or null.</exception>
    /// <exception cref="NotFoundException">Thrown when the manifest does not exist in the store.</exception>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        if (IsClosedSet())
        {
            throw new StoreClosedException();
        }

        if (string.IsNullOrEmpty(reference))
        {
            throw new MissingReferenceException();
        }

        var exists = await ExistsAsync(descriptor, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw new NotFoundException($"{descriptor.Digest}: {descriptor.MediaType}");
        }

        await _resolver.TagAsync(descriptor, reference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true if the given name exists in the file store.
    /// </summary>
    private bool NameExists(string name)
    {
        if (!_nameToStatus.TryGetValue(name, out var status))
        {
            return false;
        }

        // For read-only check, we can safely read the volatile bool without the semaphore
        // since Exists is only ever set from false to true (never reset)
        return status.Exists;
    }

    /// <summary>
    /// Returns the absolute path for the given path.
    /// </summary>
    private string GetAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.Combine(_workingDir, path);
    }

    /// <summary>
    /// Generates a descriptor from the given file.
    /// </summary>
    private async Task<Descriptor> GenerateDescriptorFromFileAsync(FileInfo fileInfo, string mediaType, string path, CancellationToken cancellationToken)
    {
        using var stream = fileInfo.OpenRead();
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var digest = $"sha256:{Convert.ToHexString(hashBytes).ToLowerInvariant()}";

        // map digest to file path
        _digestToPath.TryAdd(digest, path);

        // generate descriptor
        if (string.IsNullOrEmpty(mediaType))
        {
            mediaType = Oci.MediaType.ImageLayer;
        }

        return new Descriptor
        {
            MediaType = mediaType,
            Digest = digest,
            Size = fileInfo.Length
        };
    }
}
