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

public class CopyOptions : CopyGraphOptions
{
    /// <summary>
    /// MapRoot maps the resolved root node to a desired root node for copy.
    /// When MapRoot is provided, the descriptor resolved from the source
    /// reference will be passed to MapRoot, and the mapped descriptor will be
    /// used as the root node for copy.
    /// </summary>
    public Func<IReadOnlyStorage, Descriptor, CancellationToken, Task<Descriptor>>? MapRoot { get; set; }

    /// <summary>
    /// Initializes a new instance of the CopyOptions class with the specified parameters.
    /// </summary>
    /// <param name="maxConcurrency">Maximum number of concurrent copy tasks. If less than or equal to 0, uses default value of 10.</param>
    /// <param name="maxMetadataBytes">Maximum size of metadata that can be cached in memory. If less than or equal to 0, uses default value of 4 MiB.</param>
    /// <param name="preCopy">Handler for descriptors before they are copied. Can return SkipNodeException to skip existing items.</param>
    /// <param name="postCopy">Handler for descriptors after they are copied.</param>
    /// <param name="onCopySkipped">Handler called when sub-DAG rooted by current node is skipped.</param>
    /// <param name="mountFrom">Function returning candidate repositories for mounting. If null, falls back to copy.</param>
    /// <param name="onMounted">Handler invoked when descriptor is mounted.</param>
    /// <param name="findSuccessors">Function to find successors of current node. If null, uses default implementation.</param>
    /// <param name="mapRoot">Function to map the resolved root node to a desired root node for copy.</param>
    public CopyOptions(
        int maxConcurrency = 0,
        long maxMetadataBytes = 0,
        Func<Descriptor, CancellationToken, Task>? preCopy = null,
        Func<Descriptor, CancellationToken, Task>? postCopy = null,
        Func<Descriptor, CancellationToken, Task>? onCopySkipped = null,
        Func<Descriptor, CancellationToken, Task<IEnumerable<string>>>? mountFrom = null,
        Func<Descriptor, CancellationToken, Task>? onMounted = null,
        Func<IFetchable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>>? findSuccessors = null,
        Func<IReadOnlyStorage, Descriptor, CancellationToken, Task<Descriptor>>? mapRoot = null)
        : base(maxConcurrency, maxMetadataBytes, preCopy, postCopy, onCopySkipped, mountFrom, onMounted, findSuccessors)
    {
        MapRoot = mapRoot;
    }
}
