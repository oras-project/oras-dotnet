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

using OrasProject.Oras.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Interfaces
{
    /// <summary>
    /// IDeleter removes content.
    /// </summary>
    public interface IDeleter
    {
        /// <summary>
        /// This deletes content Identified by the descriptor
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default);
    }
}
