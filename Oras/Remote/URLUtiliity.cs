namespace Oras.Remote
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
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRegistryBaseURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildScheme(plainHTTP)}://{@ref.Host()}/v2/";
        }

        /// <summary>
        /// BuildManifestURL builds the URL for accessing the catalog API.
        /// Format: <scheme>://<registry>/v2/_catalog
        /// Reference: https://docs.docker.com/registry/spec/api/#catalog
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRegistryCatalogURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildScheme(plainHTTP)}://{@ref.Host()}/v2/_catalog";
        }

        /// <summary>
        /// BuildRepositoryBaseURL builds the base endpoint of the remote repository.
        /// Format: <scheme>://<registry>/v2/<repository>
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBaseURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildScheme(plainHTTP)}://{@ref.Host()}/v2/{@ref.Repository}";
        }

        /// <summary>
        /// BuildRepositoryTagListURL builds the URL for accessing the tag list API.
        /// Format: <scheme>://<registry>/v2/<repository>/tags/list
        /// Reference: https://docs.docker.com/registry/spec/api/#tags
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRepositoryTagListURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildScheme(plainHTTP)}://{@ref.Host()}/v2/{@ref.Repository}/tags/list";
        }

        /// <summary>
        /// BuildRepositoryManifestURL builds the URL for accessing the manifest API.
        /// Format: <scheme>://<registry>/v2/<repository>/manifests/<digest_or_tag>
        /// Reference: https://docs.docker.com/registry/spec/api/#manifest
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRepositoryManifestURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, @ref)}/manifests/{@ref.Reference}";
        }

        /// <summary>
        /// BuildRepositoryBlobURL builds the URL for accessing the blob API.
        /// Format: <scheme>://<registry>/v2/<repository>/blobs/<digest>
        /// Reference: https://docs.docker.com/registry/spec/api/#blob
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBlobURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, @ref)}/blobs/{@ref.Reference}";
        }

        /// <summary>
        /// BuildRepositoryBlobUploadURL builds the URL for accessing the blob upload API.
        /// Format: <scheme>://<registry>/v2/<repository>/blobs/uploads/
        /// Reference: https://docs.docker.com/registry/spec/api/#initiate-blob-upload

        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="ref"></param>
        /// <returns></returns>
        internal static string BuildRepositoryBlobUploadURL(bool plainHTTP, RemoteReference @ref)
        {
            return $"{BuildRepositoryBaseURL(plainHTTP, @ref)}/blobs/uploads/";
        }

    }
}
