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

using OrasProject.Oras.Remote;
using System.Net.Http;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// IRepositoryOption is used to configure a remote repository.
/// </summary>
public interface IRepositoryOption
{
    /// <summary>
    /// Client is the underlying HTTP client used to access the remote registry.
    /// </summary>
    public HttpClient HttpClient { get; set; }

    /// <summary>
    /// Reference references the remote repository.
    /// </summary>
    public RemoteReference RemoteReference { get; set; }

    /// <summary>
    /// PlainHTTP signals the transport to access the remote repository via HTTP
    /// instead of HTTPS.
    /// </summary>
    public bool PlainHTTP { get; set; }


    /// <summary>
    /// ManifestMediaTypes is used in `Accept` header for resolving manifests
    /// from references. It is also used in identifying manifests and blobs from
    /// descriptors. If an empty list is present, default manifest media types
    /// are used.
    /// </summary>
    public string[] ManifestMediaTypes { get; set; }

    /// <summary>
    /// TagListPageSize specifies the page size when invoking the tag list API.
    /// If zero, the page size is determined by the remote registry.
    /// Reference: https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    public int TagListPageSize { get; set; }

}
