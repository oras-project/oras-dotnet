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

namespace OrasProject.Oras.Remote
{

    internal static class URLUtiliity
    {
        /// <summary>
        /// BuildScheme returns HTTP scheme used to access the remote registry.
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <returns></returns>
        internal static string BuildScheme(bool plainHTTP)
        {
            if (plainHTTP)
            {
                return "http";
            }

            return "https";
        }

        /// <summary>
        /// BuildRegistryBaseURL builds the URL for accessing the base API.
        /// Format: <scheme>://<registry>/v2/
        /// Reference: https://docs.docker.com/registry/spec/api/#base
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRegistryBaseURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildScheme(plainHTTP)}://{reference.Host()}/v2/";
        }

        /// <summary>
        /// BuildManifestURL builds the URL for accessing the catalog API.
        /// Format: <scheme>://<registry>/v2/_catalog
        /// Reference: https://docs.docker.com/registry/spec/api/#catalog
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRegistryCatalogURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildScheme(plainHTTP)}://{reference.Host()}/v2/_catalog";
        }

        /// <summary>
        /// BuildRepositoryBaseURL builds the base endpoint of the remote repository.
        /// Format: <scheme>://<registry>/v2/<repository>
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBaseURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildScheme(plainHTTP)}://{reference.Host()}/v2/{reference.Repository}";
        }

        /// <summary>
        /// BuildRepositoryTagListURL builds the URL for accessing the tag list API.
        /// Format: <scheme>://<registry>/v2/<repository>/tags/list
        /// Reference: https://docs.docker.com/registry/spec/api/#tags
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRepositoryTagListURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, reference)}/tags/list";
        }

        /// <summary>
        /// BuildRepositoryManifestURL builds the URL for accessing the manifest API.
        /// Format: <scheme>://<registry>/v2/<repository>/manifests/<digest_or_tag>
        /// Reference: https://docs.docker.com/registry/spec/api/#manifest
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRepositoryManifestURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, reference)}/manifests/{reference.Reference}";
        }

        /// <summary>
        /// BuildRepositoryBlobURL builds the URL for accessing the blob API.
        /// Format: <scheme>://<registry>/v2/<repository>/blobs/<digest>
        /// Reference: https://docs.docker.com/registry/spec/api/#blob
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBlobURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, reference)}/blobs/{reference.Reference}";
        }

        /// <summary>
        /// BuildRepositoryBlobUploadURL builds the URL for accessing the blob upload API.
        /// Format: <scheme>://<registry>/v2/<repository>/blobs/uploads/
        /// Reference: https://docs.docker.com/registry/spec/api/#initiate-blob-upload

        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBlobUploadURL(bool plainHTTP, RemoteReference reference)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, reference)}/blobs/uploads/";
        }

    }
}
