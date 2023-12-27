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

namespace OrasProject.Oras.Interfaces.Registry
{
    /// <summary>
    /// Repository is an ORAS target and an union of the blob and the manifest CASs.
    /// As specified by https://docs.docker.com/registry/spec/api/, it is natural to
    /// assume that IResolver interface only works for manifests. Tagging a
    /// blob may be resulted in an `UnsupportedException` error. However, this interface
    /// does not restrict tagging blobs.
    /// Since a repository is an union of the blob and the manifest CASs, all
    /// operations defined in the `IBlobStore` are executed depending on the media
    /// type of the given descriptor accordingly.
    /// Furthermore, this interface also provides the ability to enforce the
    /// separation of the blob and the manifests CASs.
    /// </summary>
    public interface IRepository : ITarget, IReferenceFetcher, IReferencePusher, IDeleter, ITagLister
    {
        /// <summary>
        /// Blobs provides access to the blob CAS only, which contains config blobs,layers, and other generic blobs.
        /// </summary>
        /// <returns></returns>
        IBlobStore Blobs();
        /// <summary>
        /// Manifests provides access to the manifest CAS only.
        /// </summary>
        /// <returns></returns>
        IManifestStore Manifests();
    }
}
