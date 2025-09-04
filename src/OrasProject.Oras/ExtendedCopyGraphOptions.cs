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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
namespace OrasProject.Oras;

/// <summary>
/// ExtendedCopyGraphOptions contains additional parameters for extended copy graph operations.
/// </summary>
public class ExtendedCopyGraphOptions : CopyGraphOptions
{
    /// <summary>
    /// Depth limits the maximum depth for finding predecessors.
    /// If Depth is 0, there is no depth limit.
    /// </summary>
    public int Depth { get; init; } = 0;

    /// <summary>
    /// FindPredecessors finds the predecessors of the current node.
    /// If FindPredecessors is null, the default implementation will be used.
    /// </summary>
    public Func<IPredecessorFindable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>>? FindPredecessors { get; init; }
}
