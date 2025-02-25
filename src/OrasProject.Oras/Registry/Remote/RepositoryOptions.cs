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

using System.Collections.Generic;
using System.Net.Http;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// RepositoryOption is used to configure a remote repository.
/// </summary>
public struct RepositoryOptions
{
    /// <summary>
    /// Client is the underlying HTTP client used to access the remote registry.
    /// </summary>
    public required HttpClient HttpClient { get; set; }

    /// <summary>
    /// Reference references the remote repository.
    /// </summary>
    public required Reference Reference { get; set; }

    /// <summary>
    /// PlainHttp signals the transport to access the remote repository via HTTP
    /// instead of HTTPS.
    /// </summary>
    public bool PlainHttp { get; set; }

    /// <summary>
    /// ManifestMediaTypes is used in `Accept` header for resolving manifests
    /// from references. It is also used in identifying manifests and blobs from
    /// descriptors. If null, default manifest media types are used.
    /// </summary>
    public IEnumerable<string>? ManifestMediaTypes { get; set; }

    /// <summary>
    /// TagListPageSize specifies the page size when invoking the tag list API.
    /// If zero, the page size is determined by the remote registry.
    /// Reference: https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    public int TagListPageSize { get; set; }

    /// <summary>
    /// SkipReferrersGc specifies whether to delete the dangling referrers
    /// index when referrers tag schema is utilized.
    ///  - If false, the old referrers index will be deleted after the new one is successfully uploaded.
    ///  - If true, the old referrers index is kept.
    /// By default, it is disabled (set to false). See also:
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#referrers-tag-schema
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#pushing-manifests-with-subject
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#deleting-manifests
    /// </summary>
    public bool SkipReferrersGc { get; set; }
    
    /// <summary>
    /// MaxMetadataBytes specifies a limit on how many response bytes are allowed
    /// in the server's response to the metadata APIs, such as catalog list, tag
    /// list, and referrers list.
    /// If less than or equal to zero, a default (currently 4MiB) is used.
    /// </summary>
    public long MaxMetadataBytes
    {
        get => _maxMetadataBytes > 0 ? _maxMetadataBytes : _defaultMaxMetadataBytes;
        set => _maxMetadataBytes = value;
    }
    
    private long _maxMetadataBytes;

    /// <summary>
    /// _defaultMaxMetadataBytes specifies the default limit on how many response
    /// bytes are allowed in the server's response to the metadata APIs.
    /// See also: Repository.MaxMetadataBytes
    /// </summary>
    private const long _defaultMaxMetadataBytes = 4 * 1024 * 1024; // 4 MiB
}
