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
/// CopyGraphOptions allows users to customize copy operation's options
/// </summary>
public class CopyGraphOptions
{
    private const int _defaultMaxConcurrency = 10;
    private const long _defaultMaxMetadataBytes = 4 * 1024 * 1024; // 4 MiB

    private int _maxConcurrency = _defaultMaxConcurrency;
    private long _maxMetadataBytes = _defaultMaxMetadataBytes;

    /// <summary>
    /// MaxConcurrency limits the maximum number of concurrent copy tasks.
    /// If less than or equal to 0, a default (currently 10) is used.
    /// </summary>
    public int Concurrency
    {
        get => _maxConcurrency;
        set
        {
            if (value > 0)
            {
                _maxConcurrency = value;
            }
            else
            {
                _maxConcurrency = _defaultMaxConcurrency;
            }
        }
    }

    /// <summary>
    /// MaxMetadataBytes limits the maximum size of the metadata that can be
    /// cached in the memory. If less than or equal to 0, a default (currently 4 MiB) is used.
    /// </summary>
    public long MaxMetadataBytes
    {
        get => _maxMetadataBytes;
        set
        {
            if (value > 0)
            {
                _maxMetadataBytes = value;
            }
            else
            {
                _maxMetadataBytes = _defaultMaxMetadataBytes;
            }
        }
    }

    /// <summary>
    /// PreCopy handles the current descriptor before it is copied. PreCopy can
    /// return CopyAction.SkipNode to signal that desc should be skipped when it already
    /// exists in the target.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task<CopyAction>>? PreCopy { get; set; }

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
    /// FindSuccessors finds the successors of the current node.
    /// IFetchable provides cached access to the source storage, and is suitable
    /// for fetching non-leaf nodes like manifests. Since anything fetched from
    /// fetcher will be cached in the memory, it is recommended to use original
    /// source storage to fetch large blobs.
    /// If FindSuccessors is not set, FetchableExtensions.GetSuccessorsAsync will be used.
    /// </summary>
    public Func<IFetchable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>> FindSuccessors { get; set; } = FetchableExtensions.GetSuccessorsAsync;
}
