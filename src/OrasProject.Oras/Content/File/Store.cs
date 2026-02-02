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

using OrasProject.Oras.Content.File.Exceptions;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content.File;

/// <summary>
/// FileStore represents a file system based store, which implements <see cref="ITarget"/>.
/// <para>
/// In the file store, the contents described by names are location-addressed
/// by file paths. Meanwhile, the file paths are mapped to a virtual CAS
/// where all metadata are stored in the memory.
/// </para>
/// <para>
/// The contents that are not described by names are stored in a fallback storage,
/// which is a limited memory CAS by default.
/// </para>
/// <para>
/// As all the metadata are stored in the memory, the file store
/// cannot be restored from the file system.
/// </para>
/// <para>
/// After use, the file store needs to be disposed by calling the <see cref="Dispose"/> method
/// or by using a using statement. The file store cannot be used after being disposed.
/// </para>
/// </summary>
public class Store : ITarget, IPredecessorFindable, IDisposable, IAsyncDisposable
{
    private const long _defaultFallbackPushSizeLimit = 1 << 22; // 4 MiB
    private const string _defaultBlobMediaType = Oci.MediaType.ImageLayer;
    private const string _defaultBlobDirMediaType = Oci.MediaType.ImageLayerGzip;

    private readonly string _workingDir;
    private readonly IStorage _fallbackStorage;
    private readonly MemoryTagStore _resolver = new();
    private readonly MemoryGraph _graph = new();
    private readonly ConcurrentDictionary<string, string> _digestToPath = new();
    private readonly ConcurrentDictionary<string, NameStatus> _nameToStatus = new();
    private readonly ConcurrentDictionary<string, bool> _tmpFiles = new();
    private int _closed;

    /// <summary>
    /// Controls if the tarballs generated for the added directories are reproducible.
    /// When specified, some metadata such as change time will be removed from the files in the tarballs.
    /// Default value: false.
    /// </summary>
    public bool TarReproducible { get; set; }

    /// <summary>
    /// Controls if path traversal is allowed when writing files.
    /// When specified, writing files outside the working directory will be allowed.
    /// Default value: false.
    /// </summary>
    public bool AllowPathTraversalOnWrite { get; set; }

    /// <summary>
    /// Controls if push operations can overwrite existing files.
    /// When specified, saving files to existing paths will be disabled.
    /// Default value: false.
    /// </summary>
    public bool DisableOverwrite { get; set; }

    /// <summary>
    /// Controls if files with same content but different names are deduped after push operations.
    /// When a DAG is copied between CAS targets, nodes are deduped by content.
    /// By default, file store restores deduped successor files after a node is copied.
    /// This may result in two files with identical content.
    /// If this is not the desired behavior, ForceCAS can be specified to enforce CAS style dedup.
    /// Default value: false.
    /// </summary>
    public bool ForceCAS { get; set; }

    /// <summary>
    /// Controls if push operations should ignore descriptors without a name.
    /// When specified, corresponding content will be discarded.
    /// Otherwise, content will be saved to a fallback storage.
    /// A typical scenario is pulling an arbitrary artifact masqueraded as OCI image to file store.
    /// This option can be specified to discard unnamed manifest and config file,
    /// while leaving only named layer files.
    /// Default value: false.
    /// </summary>
    public bool IgnoreNoName { get; set; }

    /// <summary>
    /// Controls if push operations should skip unpacking files.
    /// This value overrides the <see cref="FileStoreAnnotations.AnnotationUnpack"/>.
    /// Default value: false.
    /// </summary>
    public bool SkipUnpack { get; set; }

    /// <summary>
    /// Controls whether to preserve file permissions when unpacking,
    /// disregarding the active umask, similar to tar's `--preserve-permissions`.
    /// Default value: false.
    /// </summary>
    public bool PreservePermissions { get; set; }

    /// <summary>
    /// Creates a file store with the default limited memory CAS as the fallback storage
    /// for contents without names. When pushing content without names, the size of content
    /// being pushed cannot exceed the default size limit: 4 MiB.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    public Store(string workingDir)
        : this(workingDir, _defaultFallbackPushSizeLimit)
    {
    }

    /// <summary>
    /// Creates a file store with a limited memory CAS as the fallback storage
    /// for contents without names. When pushing content without names, the size
    /// of content being pushed cannot exceed the specified size limit.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <param name="fallbackLimit">The size limit for the fallback storage.</param>
    public Store(string workingDir, long fallbackLimit)
        : this(workingDir, new LimitedStorage(new MemoryStorage(), fallbackLimit))
    {
    }

    /// <summary>
    /// Creates a file store with the provided fallback storage for contents without names.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <param name="fallbackStorage">The fallback storage for unnamed content.</param>
    public Store(string workingDir, IStorage fallbackStorage)
    {
        _workingDir = Path.GetFullPath(workingDir);
        _fallbackStorage = fallbackStorage;
    }

    /// <summary>
    /// Fetches the content identified by the descriptor.
    /// </summary>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        // If the target has a name, check if the name exists
        var name = GetAnnotationTitle(target);
        if (!string.IsNullOrEmpty(name) && !NameExists(name))
        {
            throw new NotFoundException($"{name}: {target.MediaType}");
        }

        // Check if the content exists in the store
        if (_digestToPath.TryGetValue(target.Digest, out var path))
        {
            if (!System.IO.File.Exists(path))
            {
                throw new NotFoundException($"{target.Digest}: {target.MediaType}");
            }
            return System.IO.File.OpenRead(path);
        }

        // Fall back to the fallback storage
        return await _fallbackStorage.FetchAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pushes the content, matching the expected descriptor.
    /// If name is not specified in the descriptor, the content will be pushed to
    /// the fallback storage by default, or will be discarded when IgnoreNoName is true.
    /// </summary>
    public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        var skipped = await PushInternalAsync(expected, contentStream, cancellationToken).ConfigureAwait(false);
        if (skipped)
        {
            return;
        }

        if (!ForceCAS)
        {
            await RestoreDuplicatesAsync(expected, cancellationToken).ConfigureAwait(false);
        }

        await _graph.IndexAsync(this, expected, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal push implementation.
    /// </summary>
    /// <returns>True if the content was skipped (unnamed and IgnoreNoName is set), false otherwise.</returns>
    private async Task<bool> PushInternalAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken)
    {
        var name = GetAnnotationTitle(expected);
        if (string.IsNullOrEmpty(name))
        {
            if (IgnoreNoName)
            {
                return true;
            }
            await _fallbackStorage.PushAsync(expected, contentStream, cancellationToken).ConfigureAwait(false);
            return false;
        }

        // Check the status of the name
        var status = GetStatus(name);
        await status.WaitLockAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (status.Exists)
            {
                throw new DuplicateNameException(name);
            }

            var targetPath = ResolveWritePath(name);

            var needUnpack = expected.Annotations?.TryGetValue(FileStoreAnnotations.AnnotationUnpack, out var unpackValue) == true
                && unpackValue == "true" && !SkipUnpack;

            if (needUnpack)
            {
                await PushDirectoryAsync(name, targetPath, expected, contentStream, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await PushFileAsync(targetPath, expected, contentStream, cancellationToken).ConfigureAwait(false);
            }

            // Update the name status as existed
            status.Exists = true;
        }
        finally
        {
            status.ReleaseLock();
        }

        return false;
    }

    /// <summary>
    /// Restores successor files with same content but different names.
    /// </summary>
    private async Task RestoreDuplicatesAsync(Descriptor desc, CancellationToken cancellationToken)
    {
        var successors = await this.GetSuccessorsAsync(desc, cancellationToken).ConfigureAwait(false);

        foreach (var successor in successors)
        {
            var name = GetAnnotationTitle(successor);
            if (string.IsNullOrEmpty(name) || NameExists(name))
            {
                continue;
            }

            try
            {
                var fetchDesc = new Descriptor
                {
                    MediaType = successor.MediaType,
                    Digest = successor.Digest,
                    Size = successor.Size
                };

                var rc = await FetchAsync(fetchDesc, cancellationToken).ConfigureAwait(false);
                await using (rc.ConfigureAwait(false))
                {
                    await PushInternalAsync(successor, rc, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (NotFoundException)
            {
                // Allow pushing manifests before blobs
            }
            catch (DuplicateNameException)
            {
                // In case multiple goroutines are pushing or restoring the same
                // named content, the error is ignored
            }
        }
    }

    /// <summary>
    /// Returns whether the described content exists.
    /// </summary>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        // If the target has a name, check if the name exists
        var name = GetAnnotationTitle(target);
        if (!string.IsNullOrEmpty(name) && !NameExists(name))
        {
            return false;
        }

        // Check if the content exists in the store
        if (_digestToPath.ContainsKey(target.Digest))
        {
            return true;
        }

        // Fall back to the fallback storage
        return await _fallbackStorage.ExistsAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a reference to a descriptor.
    /// </summary>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        if (string.IsNullOrEmpty(reference))
        {
            throw new ArgumentException("Missing reference", nameof(reference));
        }

        return await _resolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tags a descriptor with a reference string.
    /// </summary>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        if (string.IsNullOrEmpty(reference))
        {
            throw new ArgumentException("Missing reference", nameof(reference));
        }

        var exists = await ExistsAsync(descriptor, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw new NotFoundException($"{descriptor.Digest}: {descriptor.MediaType}");
        }

        await _resolver.TagAsync(descriptor, reference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the nodes directly pointing to the current node.
    /// Predecessors returns an empty collection without error if the node does not exist in the store.
    /// </summary>
    public async Task<IEnumerable<Descriptor>> GetPredecessorsAsync(Descriptor node, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        return await _graph.GetPredecessorsAsync(node, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a file or a directory into the file store.
    /// Hard links within the directory are treated as regular files.
    /// </summary>
    /// <param name="name">The name to assign to the content.</param>
    /// <param name="mediaType">The media type of the content. If empty, defaults are used.</param>
    /// <param name="path">The path to the file or directory. If empty, name is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The descriptor of the added content.</returns>
    public async Task<Descriptor> AddAsync(string name, string mediaType, string path, CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();

        if (string.IsNullOrEmpty(name))
        {
            throw new MissingNameException();
        }

        // Check the status of the name
        var status = GetStatus(name);
        await status.WaitLockAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (status.Exists)
            {
                throw new DuplicateNameException(name);
            }

            if (string.IsNullOrEmpty(path))
            {
                path = name;
            }
            path = AbsPath(path);

            var fileInfo = new FileInfo(path);
            var dirInfo = new DirectoryInfo(path);

            Descriptor desc;
            if (dirInfo.Exists)
            {
                desc = await DescriptorFromDirectoryAsync(name, mediaType, path, cancellationToken).ConfigureAwait(false);
            }
            else if (fileInfo.Exists)
            {
                desc = await DescriptorFromFileAsync(fileInfo, mediaType, path, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new FileNotFoundException($"Failed to stat {path}", path);
            }

            // Add the annotation title
            desc.Annotations ??= new Dictionary<string, string>();
            desc.Annotations[Oci.Annotations.Title] = name;

            // Update the name status as existed
            status.Exists = true;
            return desc;
        }
        finally
        {
            status.ReleaseLock();
        }
    }

    /// <summary>
    /// Saves content matching the descriptor to the given file.
    /// </summary>
    private async Task SaveFileAsync(FileStream fp, Descriptor expected, Stream contentStream, CancellationToken cancellationToken)
    {
        var path = fp.Name;

        try
        {
            await using (fp)
            {
                await VerifyAndCopyAsync(fp, contentStream, expected, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Clean up on failure
            try { System.IO.File.Delete(path); } catch { /* ignore */ }
            throw;
        }

        _digestToPath[expected.Digest] = path;
    }

    /// <summary>
    /// Saves content matching the descriptor to the target path.
    /// </summary>
    private async Task PushFileAsync(string targetPath, Descriptor expected, Stream contentStream, CancellationToken cancellationToken)
    {
        var parentDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        var fp = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await SaveFileAsync(fp, expected, contentStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves content matching the descriptor to the target directory.
    /// </summary>
    private async Task PushDirectoryAsync(string name, string targetPath, Descriptor expected, Stream contentStream, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetPath);

        // Create a temp file to store the gzip
        var (tempFile, tempPath) = await CreateTempFileAsync(cancellationToken).ConfigureAwait(false);

        // Save the gzip content (verifying digest while saving)
        await SaveFileAsync(tempFile, expected, contentStream, cancellationToken).ConfigureAwait(false);

        // Get the checksum for the uncompressed content
        string? checksum = null;
        expected.Annotations?.TryGetValue(FileStoreAnnotations.AnnotationDigest, out checksum);

        // Extract the tar.gz
        await TarUtilities.ExtractTarGzipAsync(
            targetPath,
            name,
            tempPath,
            checksum,
            PreservePermissions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a descriptor from a directory by creating a tar.gz archive.
    /// </summary>
    private async Task<Descriptor> DescriptorFromDirectoryAsync(string name, string mediaType, string dirPath, CancellationToken cancellationToken)
    {
        // Create a temp file to store the gzip
        var (tempFile, tempPath) = await CreateTempFileAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var gzDigester = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var tarDigester = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var gzFileStream = tempFile;
            await using (gzFileStream.ConfigureAwait(false))
            {
                var gzHashingStream = new HashingStream(gzFileStream, gzDigester);
                await using (gzHashingStream.ConfigureAwait(false))
                {
                    var gzipStream = new GZipStream(gzHashingStream, CompressionLevel.Optimal, leaveOpen: true);
                    await using (gzipStream.ConfigureAwait(false))
                    {
                        var tarHashingStream = new HashingStream(gzipStream, tarDigester);
                        await using (tarHashingStream.ConfigureAwait(false))
                        {
                            await TarUtilities.TarDirectoryAsync(dirPath, name, tarHashingStream, TarReproducible, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await gzFileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Get file size
            var fileInfo = new FileInfo(tempPath);

            // Map gzip digest to gzip path
            var gzDigest = $"sha256:{Convert.ToHexString(gzDigester.GetHashAndReset()).ToLowerInvariant()}";
            _digestToPath[gzDigest] = tempPath;

            // Generate descriptor
            if (string.IsNullOrEmpty(mediaType))
            {
                mediaType = _defaultBlobDirMediaType;
            }

            var tarDigest = $"sha256:{Convert.ToHexString(tarDigester.GetHashAndReset()).ToLowerInvariant()}";

            return new Descriptor
            {
                MediaType = mediaType,
                Digest = gzDigest,
                Size = fileInfo.Length,
                Annotations = new Dictionary<string, string>
                {
                    [FileStoreAnnotations.AnnotationDigest] = tarDigest,
                    [FileStoreAnnotations.AnnotationUnpack] = "true"
                }
            };
        }
        catch
        {
            // Clean up on failure
            await tempFile.DisposeAsync().ConfigureAwait(false);
            try { System.IO.File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>
    /// Generates a descriptor from a file.
    /// </summary>
    private async Task<Descriptor> DescriptorFromFileAsync(FileInfo fileInfo, string mediaType, string filePath, CancellationToken cancellationToken)
    {
        var fp = System.IO.File.OpenRead(filePath);
        await using (fp.ConfigureAwait(false))
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await fp.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.AppendData(buffer.AsSpan(0, bytesRead));
            }

            var digest = $"sha256:{Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant()}";

            // Map digest to file path
            _digestToPath[digest] = filePath;

            // Generate descriptor
            if (string.IsNullOrEmpty(mediaType))
            {
                mediaType = _defaultBlobMediaType;
            }

            return new Descriptor
            {
                MediaType = mediaType,
                Digest = digest,
                Size = fileInfo.Length
            };
        }
    }

    /// <summary>
    /// Resolves the path to write for the given name.
    /// </summary>
    private string ResolveWritePath(string name)
    {
        var path = AbsPath(name);

        if (!AllowPathTraversalOnWrite)
        {
            var basePath = Path.GetFullPath(_workingDir);
            var targetPath = Path.GetFullPath(path);

            var relativePath = Path.GetRelativePath(basePath, targetPath);
            var cleanPath = relativePath.Replace('\\', '/');
            if (cleanPath.StartsWith("../", StringComparison.Ordinal) || cleanPath == "..")
            {
                throw new PathTraversalDisallowedException();
            }
        }

        if (DisableOverwrite)
        {
            if (System.IO.File.Exists(path))
            {
                throw new OverwriteDisallowedException();
            }
        }

        return path;
    }

    /// <summary>
    /// Gets the status object for a name.
    /// </summary>
    private NameStatus GetStatus(string name)
    {
        return _nameToStatus.GetOrAdd(name, _ => new NameStatus());
    }

    /// <summary>
    /// Returns whether the given name exists in the file store.
    /// </summary>
    private bool NameExists(string name)
    {
        var status = GetStatus(name);
        return status.ExistsWithoutLock;
    }

    /// <summary>
    /// Creates a temporary file.
    /// </summary>
    private Task<(FileStream, string)> CreateTempFileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tempPath = Path.Combine(Path.GetTempPath(), $"oras_file_{Guid.NewGuid():N}");
        var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _tmpFiles[tempPath] = true;
        return Task.FromResult((stream, tempPath));
    }

    /// <summary>
    /// Returns the absolute path of the path.
    /// </summary>
    private string AbsPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.Combine(_workingDir, path);
    }

    /// <summary>
    /// Throws if the store is closed.
    /// </summary>
    private void ThrowIfClosed()
    {
        if (Interlocked.CompareExchange(ref _closed, 0, 0) == 1)
        {
            throw new FileStoreClosedException();
        }
    }

    /// <summary>
    /// Gets the annotation title from a descriptor.
    /// </summary>
    private static string? GetAnnotationTitle(Descriptor descriptor)
    {
        string? title = null;
        descriptor.Annotations?.TryGetValue(Oci.Annotations.Title, out title);
        return title;
    }

    /// <summary>
    /// Verifies and copies content from source to destination.
    /// </summary>
    private static async Task VerifyAndCopyAsync(Stream destination, Stream source, Descriptor expected, CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            hasher.AppendData(buffer.AsSpan(0, bytesRead));
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytes += bytesRead;
        }

        if (totalBytes != expected.Size)
        {
            throw new Content.Exceptions.MismatchedSizeException(
                $"Descriptor size {expected.Size} is different from content size {totalBytes}");
        }

        var digest = $"sha256:{Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant()}";
        if (!string.Equals(digest, expected.Digest, StringComparison.OrdinalIgnoreCase))
        {
            throw new Content.Exceptions.MismatchedDigestException(
                $"Descriptor digest {expected.Digest} is different from content digest {digest}");
        }
    }

    /// <summary>
    /// Disposes the file store and cleans up all temporary files.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        foreach (var tempPath in _tmpFiles.Keys)
        {
            try
            {
                System.IO.File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Asynchronously disposes the file store and cleans up all temporary files.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Tracks the status of a named content entry.
    /// </summary>
    private sealed class NameStatus
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _exists;

        public bool Exists
        {
            get
            {
                return _exists;
            }
            set
            {
                _exists = value;
            }
        }

        public bool ExistsWithoutLock => Volatile.Read(ref _exists);

        public async Task WaitLockAsync(CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void ReleaseLock()
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// A stream wrapper that computes a hash while writing.
    /// </summary>
    private sealed class HashingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IncrementalHash _hasher;

        public HashingStream(Stream inner, IncrementalHash hasher)
        {
            _inner = inner;
            _hasher = hasher;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _hasher.AppendData(buffer.AsSpan(offset, count));
            _inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _hasher.AppendData(buffer.AsSpan(offset, count));
            await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _hasher.AppendData(buffer.Span);
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}
