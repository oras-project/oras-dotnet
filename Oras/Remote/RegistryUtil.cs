using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Remote
{
    
    internal static class RegistryUtil
    {
        /// <summary>
        /// BuildScheme returns HTTP scheme used to access the remote registry.
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <returns></returns>
         public static string BuildScheme(bool plainHTTP)
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
        /// <param name="refObj"></param>
        /// <returns></returns>
        public static string BuildRegistryBaseURL(bool plainHTTP, ReferenceObj refObj)
        {
            return $"{BuildScheme(plainHTTP)}://{refObj.Host()}/v2/";
        }

        /// <summary>
        /// BuildManifestURL builds the URL for accessing the catalog API.
        /// Format: <scheme>://<registry>/v2/_catalog
        /// Reference: https://docs.docker.com/registry/spec/api/#catalog
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="refObj"></param>
        /// <returns></returns>
        public static string BuildRegistryCatalogURL(bool plainHTTP, ReferenceObj refObj)
        {
            return $"{BuildScheme(plainHTTP)}://{refObj.Host()}/v2/_catalog";
        }

        /// <summary>
        /// BuildRepositoryBaseURL builds the base endpoint of the remote repository.
        /// Format: <scheme>://<registry>/v2/<repository>
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="refObj"></param>
        /// <returns></returns>
        public static string BuildRepositoryBaseURL(bool plainHTTP, ReferenceObj refObj)
        {
            return $"{BuildScheme(plainHTTP)}://{refObj.Host()}/v2/{refObj.Repository}/";
        }

        /// <summary>
        /// BuildRepositoryTagListURL builds the URL for accessing the tag list API.
        /// Format: <scheme>://<registry>/v2/<repository>/tags/list
        /// Reference: https://docs.docker.com/registry/spec/api/#tags
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <param name="refObj"></param>
        /// <returns></returns>
        public static string BuildRepositoryTagListURL(bool plainHTTP, ReferenceObj refObj)
        {
            return $"{BuildScheme(plainHTTP)}://{refObj.Host()}/v2/{refObj.Repository}/tags/list";
        }

        

    }
}
