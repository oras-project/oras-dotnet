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

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// Specifies how blobs are uploaded to a remote repository.
/// </summary>
public enum BlobUploadMode
{
    /// <summary>
    /// Upload the blob in a single <c>PUT</c> request after initiating the upload session.
    /// </summary>
    Monolithic,

    /// <summary>
    /// Upload the blob using one or more <c>PATCH</c> requests and finalize the upload with
    /// a <c>PUT</c> request.
    /// </summary>
    Chunked,

    /// <summary>
    /// Attempt a monolithic upload first and retry using a new chunked upload session when
    /// the registry rejects the monolithic request. The source stream must be seekable for
    /// the retry to be attempted.
    /// </summary>
    MonolithicWithChunkedFallback,
}
