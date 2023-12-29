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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Interfaces
{
    /// <summary>
    /// IStorage represents a content-addressable storage (CAS) where contents are accessed via Descriptors.
    /// The storage is designed to handle blobs of large sizes.
    /// </summary>
    public interface IStorage : IReadOnlyStorage
    {
        /// <summary>
        /// PushAsync pushes the content, matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default);
    }
}
