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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

/// <summary>
/// CopyGraphOptions contains parameters for oras.CopyGraph.
/// </summary>
public class CopyGraphOptions
{
    protected const int _defaultConcurrency = 10;
    protected const long _defaultMaxMetadataBytes = 4 * 1024 * 1024; // 4 MiB

    /// <summary>
    /// MaxConcurrency limits the maximum number of concurrent copy tasks.
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// MaxMetadataBytes limits the maximum size of the metadata that can be
    /// cached in the memory.
    /// </summary>
    public long MaxMetadataBytes { get; init; }

    /// <summary>
    /// PreCopy handles the current descriptor before it is copied. PreCopy can
    /// return a SkipNodeException to signal that desc should be skipped when it already
    /// exists in the target.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? PreCopy { get; set; }

    /// <summary>
    /// PostCopy handles the current descriptor after it is copied.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? PostCopy { get; set; }

    /// <summary>
    /// OnCopySkipped will be called when the sub-DAG rooted by the current node
    /// is skipped.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? OnCopySkipped { get; set; }

    /// <summary>
    /// MountFrom returns the candidate repositories that desc may be mounted from.
    /// The OCI references will be tried in turn. If mounting fails on all of them,
    /// then it falls back to a copy.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task<IEnumerable<string>>>? MountFrom { get; set; }

    /// <summary>
    /// OnMounted will be invoked when desc is mounted.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? OnMounted { get; set; }

    /// <summary>
    /// FindSuccessors finds the successors of the current node.
    /// IFetchable provides cached access to the source storage, and is suitable
    /// for fetching non-leaf nodes like manifests. Since anything fetched from
    /// fetcher will be cached in the memory, it is recommended to use original
    /// source storage to fetch large blobs.
    /// If FindSuccessors is not set, FetchableExtensions.GetSuccessorsAsync will be used.
    /// </summary>
    public Func<IFetchable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>> FindSuccessors { get; set; }

    /// <summary>
    /// Initializes a new instance of the CopyGraphOptions class with the specified parameters.
    /// </summary>
    /// <param name="maxConcurrency">Maximum number of concurrent copy tasks. If less than or equal to 0, a default (currently 10) is used.</param>
    /// <param name="maxMetadataBytes">Maximum size of metadata that can be cached in memory. If less than or equal to 0, a default (currently 4 MiB) is used.</param>
    /// <param name="preCopy">Handler for descriptors before they are copied. Can return SkipNodeException to skip existing items.</param>
    /// <param name="postCopy">Handler for descriptors after they are copied.</param>
    /// <param name="onCopySkipped">Handler called when sub-DAG rooted by current node is skipped.</param>
    /// <param name="mountFrom">Function returning candidate repositories for mounting. If null, falls back to copy.</param>
    /// <param name="onMounted">Handler invoked when descriptor is mounted.</param>
    /// <param name="findSuccessors">Function to find successors of current node. If null, uses default implementation.</param>
    public CopyGraphOptions(
        int maxConcurrency = _defaultConcurrency,
        long maxMetadataBytes = _defaultMaxMetadataBytes,
        Func<Descriptor, CancellationToken, Task>? preCopy = null,
        Func<Descriptor, CancellationToken, Task>? postCopy = null,
        Func<Descriptor, CancellationToken, Task>? onCopySkipped = null,
        Func<Descriptor, CancellationToken, Task<IEnumerable<string>>>? mountFrom = null,
        Func<Descriptor, CancellationToken, Task>? onMounted = null,
        Func<IFetchable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>>? findSuccessors = null)
    {
        MaxConcurrency = maxConcurrency > 0 ? maxConcurrency : _defaultConcurrency;
        MaxMetadataBytes = maxMetadataBytes > 0 ? maxMetadataBytes : _defaultMaxMetadataBytes;
        PreCopy = preCopy;
        PostCopy = postCopy;
        OnCopySkipped = onCopySkipped;
        MountFrom = mountFrom;
        OnMounted = onMounted;
        FindSuccessors = findSuccessors ?? FetchableExtensions.GetSuccessorsAsync;
    }
}
