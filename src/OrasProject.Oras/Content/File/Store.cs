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

namespace OrasProject.Oras.Content.File;

/// <summary>
/// Provides implementation of a content store based on file system.
/// </summary>
internal static class StoreConstants
{
    internal const long DefaultFallbackPushSizeLimit = 1 << 22; // 4 MiB
}

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
    private readonly string _workingDir; // the working directory of the file store
    private int _closed; // if the store is closed - 0: false, 1: true.
    private readonly ConcurrentDictionary<string, bool> _tmpFiles = new();

    private readonly IStorage _fallbackStorage;
    private readonly ITagStore _resolver;
    private readonly MemoryGraph _graph;

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
        : this(workingDir, StoreConstants.DefaultFallbackPushSizeLimit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Store"/> class, using a default
    /// limited memory CAS as the fallback storage for contents without names.
    /// </summary>
    /// <param name="workingDir">The working directory of the file store.</param>
    /// <param name="limit">The maximum size (in bytes) for pushed content without names.</param>
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="workingDir"/> cannot be resolved to an absolute path.</exception>
    public Store(string workingDir, IStorage fallbackStorage)
    {
        var workingDirAbs = Path.GetFullPath(workingDir);

        _workingDir = workingDirAbs;
        _fallbackStorage = fallbackStorage;
        _resolver = new MemoryTagStore();
        _graph = new MemoryGraph();
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

    // private static IStorage CreateLimitedStorage(long limit)
    // {
    //     var memory = new MemoryStorage();
    //     return new LimitedStorage(memory, limit);
    // }

    /// <summary>
    /// Returns true if the `closed` flag is set, otherwise returns false.
    /// </summary>
    private bool IsClosedSet()
    {
        return Interlocked.CompareExchange(ref _closed, 0, 0) == 1;
    }

    /// <summary>
    /// Sets the `closed` flag.
    /// </summary>
    private void SetClosed()
    {
        Interlocked.Exchange(ref _closed, 1);
    }
}
