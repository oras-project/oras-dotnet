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

using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry;

/// <summary>
/// IBlobStore is a CAS with the ability to stat and delete its content.
/// </summary>
public interface IBlobStore : IStorage, IResolvable, IDeletable, IReferenceFetchable
{
    /// <summary>
    /// GetBlobLocationAsync retrieves the location URL for a blob without downloading its content.
    /// Most OCI 1.0 compatible registries return a redirect with a blob location in the header
    /// instead of returning the content directly on a /v2/blobs/sha256:<digest> request.
    /// This method captures that location URL.
    /// 
    /// Returns null if the registry returns the content directly (HTTP 200) instead of a redirect.
    /// </summary>
    /// <param name="target">The descriptor identifying the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blob location URL if a redirect is returned, otherwise null</returns>
    /// <exception cref="Exceptions.NotFoundException">Thrown when the blob is not found</exception>
    /// <exception cref="Exceptions.ResponseException">Thrown when the request fails</exception>
    Task<Uri?> GetBlobLocationAsync(Descriptor target, CancellationToken cancellationToken = default);
}
