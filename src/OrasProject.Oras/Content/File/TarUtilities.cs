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
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content.File;

/// <summary>
/// Utilities for creating and extracting tar and gzip archives.
/// </summary>
internal static class TarUtilities
{
    private const int _bufferSize = 1 << 20; // 1 MiB buffer

    /// <summary>
    /// Creates a tar archive from a directory and writes it to a stream.
    /// </summary>
    /// <param name="sourceDirectory">The source directory to archive.</param>
    /// <param name="prefix">The prefix for entries in the tar archive.</param>
    /// <param name="outputStream">The output stream to write the tar archive to.</param>
    /// <param name="reproducible">Whether to remove timestamps for reproducibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task TarDirectoryAsync(
        string sourceDirectory,
        string prefix,
        Stream outputStream,
        bool reproducible,
        CancellationToken cancellationToken = default)
    {
        var tarWriter = new TarWriter(outputStream, leaveOpen: true);
        await using var _ = tarWriter.ConfigureAwait(false);

        // Get all entries: directories and files
        var allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var allDirs = Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories);

        // First add the root directory entry if needed
        var rootEntry = CreateDirectoryEntry(prefix, sourceDirectory, reproducible);
        await tarWriter.WriteEntryAsync(rootEntry, cancellationToken).ConfigureAwait(false);

        // Add all directories
        foreach (var dir in allDirs)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, dir);
            var entryName = Path.Combine(prefix, relativePath).Replace('\\', '/');
            if (!entryName.EndsWith('/'))
            {
                entryName += '/';
            }

            var entry = CreateDirectoryEntry(entryName, dir, reproducible);
            await tarWriter.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        // Add all files
        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var entryName = Path.Combine(prefix, relativePath).Replace('\\', '/');

            var fileInfo = new FileInfo(file);

            // Check if it's a symlink
            if (fileInfo.LinkTarget != null)
            {
                var entry = new PaxTarEntry(TarEntryType.SymbolicLink, entryName)
                {
                    LinkName = fileInfo.LinkTarget,
                    Uid = 0,
                    Gid = 0
                };

                if (reproducible)
                {
                    entry.ModificationTime = default;
                }

                await tarWriter.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = System.IO.File.OpenRead(file),
                    Uid = 0,
                    Gid = 0,
                    Mode = GetUnixFileMode(fileInfo)
                };

                if (reproducible)
                {
                    entry.ModificationTime = default;
                }

                await tarWriter.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static PaxTarEntry CreateDirectoryEntry(string entryName, string directoryPath, bool reproducible)
    {
        if (!entryName.EndsWith('/'))
        {
            entryName += '/';
        }

        var dirInfo = new DirectoryInfo(directoryPath);
        var entry = new PaxTarEntry(TarEntryType.Directory, entryName)
        {
            Uid = 0,
            Gid = 0,
            Mode = GetUnixFileMode(dirInfo)
        };

        if (reproducible)
        {
            entry.ModificationTime = default;
        }

        return entry;
    }

    private static UnixFileMode GetUnixFileMode(FileSystemInfo info)
    {
        // Default to rwxr-xr-x for directories and rw-r--r-- for files
        if (info is DirectoryInfo)
        {
            return UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                   UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                   UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        }
        return UnixFileMode.UserRead | UnixFileMode.UserWrite |
               UnixFileMode.GroupRead |
               UnixFileMode.OtherRead;
    }

    /// <summary>
    /// Extracts a gzip compressed tar archive to a directory.
    /// </summary>
    /// <param name="targetDirectory">The directory to extract files to.</param>
    /// <param name="directoryName">The expected directory name prefix in the archive.</param>
    /// <param name="gzipPath">The path to the gzip file.</param>
    /// <param name="checksum">Optional checksum to verify the uncompressed content.</param>
    /// <param name="preservePermissions">Whether to preserve file permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task ExtractTarGzipAsync(
        string targetDirectory,
        string directoryName,
        string gzipPath,
        string? checksum,
        bool preservePermissions,
        CancellationToken cancellationToken = default)
    {
        var gzipStream = System.IO.File.OpenRead(gzipPath);
        await using var _1 = gzipStream.ConfigureAwait(false);
        var decompressedStream = new GZipStream(gzipStream, CompressionMode.Decompress);
        await using var _2 = decompressedStream.ConfigureAwait(false);

        Stream sourceStream = decompressedStream;
        IncrementalHash? hash = null;
        MemoryStream? bufferedStream = null;

        try
        {
            // If we have a checksum, we need to verify the content
            if (!string.IsNullOrEmpty(checksum))
            {
                hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                // Read the entire decompressed content for hashing
                bufferedStream = new MemoryStream();
                var buffer = new byte[_bufferSize];
                int bytesRead;
                while ((bytesRead = await decompressedStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer.AsSpan(0, bytesRead));
                    await bufferedStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }

                // Verify the checksum
                var computedHash = hash.GetHashAndReset();
                var computedDigest = $"sha256:{Convert.ToHexString(computedHash).ToLowerInvariant()}";
                if (!string.Equals(computedDigest, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Content digest mismatch: expected {checksum}, got {computedDigest}");
                }

                bufferedStream.Position = 0;
                sourceStream = bufferedStream;
            }

            await ExtractTarDirectoryAsync(
                targetDirectory,
                directoryName,
                sourceStream,
                preservePermissions,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            hash?.Dispose();
            if (bufferedStream != null)
            {
                await bufferedStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Extracts tar entries to a directory.
    /// </summary>
    private static async Task ExtractTarDirectoryAsync(
        string targetDirectory,
        string directoryName,
        Stream tarStream,
        bool preservePermissions,
        CancellationToken cancellationToken = default)
    {
        var tarReader = new TarReader(tarStream, leaveOpen: true);
        await using var _tar = tarReader.ConfigureAwait(false);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryName = entry.Name;

            // Resolve relative path from the expected directory prefix
            var relativePath = ResolveRelativeToBase(targetDirectory, directoryName, entryName);
            if (relativePath == null)
            {
                continue; // Skip entries outside the expected directory
            }

            var fullPath = Path.Combine(targetDirectory, relativePath);

            switch (entry.EntryType)
            {
                case TarEntryType.RegularFile:
                    // Ensure parent directory exists
                    var parentDir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    if (entry.DataStream != null)
                    {
                        var fileStream = System.IO.File.Create(fullPath);
                        await using var _fs = fileStream.ConfigureAwait(false);
                        await entry.DataStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                    }

                    if (preservePermissions && entry.Mode != default)
                    {
                        System.IO.File.SetUnixFileMode(fullPath, entry.Mode);
                    }
                    break;

                case TarEntryType.Directory:
                    Directory.CreateDirectory(fullPath);
                    if (preservePermissions && entry.Mode != default)
                    {
                        System.IO.File.SetUnixFileMode(fullPath, entry.Mode);
                    }
                    break;

                case TarEntryType.SymbolicLink:
                    var target = EnsureLinkPath(targetDirectory, directoryName, fullPath, entry.LinkName);
                    if (target != null)
                    {
                        // Ensure parent directory exists
                        var symParentDir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(symParentDir))
                        {
                            Directory.CreateDirectory(symParentDir);
                        }

                        // Remove existing file if it exists
                        if (System.IO.File.Exists(fullPath) || Directory.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }

                        System.IO.File.CreateSymbolicLink(fullPath, target);
                    }
                    break;

                case TarEntryType.HardLink:
                    // NOTE: .NET 8 doesn't have File.CreateHardLink, so we skip hard links
                    // Hard links are treated as regular files when they are first encountered
                    // and the tar archive stores them with their content.
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves the relative path of a target ensuring it stays within the base path.
    /// </summary>
    private static string? ResolveRelativeToBase(string baseAbs, string baseRel, string target)
    {
        var basePath = Path.IsPathRooted(target) ? baseAbs : baseRel;

        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(basePath, target);
        }
        catch
        {
            return null;
        }

        var cleanPath = relativePath.Replace('\\', '/');
        if (cleanPath == ".." || cleanPath.StartsWith("../", StringComparison.Ordinal))
        {
            return null;
        }

        return relativePath;
    }

    /// <summary>
    /// Ensures a link path is valid and within the base directory.
    /// </summary>
    private static string? EnsureLinkPath(string baseAbs, string baseRel, string link, string target)
    {
        var path = target;
        if (!Path.IsPathRooted(target))
        {
            var linkDir = Path.GetDirectoryName(link);
            path = linkDir != null ? Path.Combine(linkDir, target) : target;
        }

        // Ensure path is under baseAbs or baseRel
        var resolved = ResolveRelativeToBase(baseAbs, baseRel, path);
        return resolved != null ? target : null;
    }
}
