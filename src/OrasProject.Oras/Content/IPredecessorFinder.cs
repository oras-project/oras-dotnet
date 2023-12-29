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

using OrasProject.Oras.Oci;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

/// <summary>
/// IPredecessorFinder finds out the nodes directly pointing to a given node of a
/// directed acyclic graph.
/// In other words, returns the "parents" of the current descriptor.
/// IPredecessorFinder is an extension of Storage.
/// </summary>
public interface IPredecessorFinder
{
    /// <summary>
    /// returns the nodes directly pointing to the current node.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<Descriptor>> GetPredecessorsAsync(Descriptor node, CancellationToken cancellationToken = default);
}
