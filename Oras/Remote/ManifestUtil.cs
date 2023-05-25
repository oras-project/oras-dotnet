using Oras.Constants;
using Oras.Models;
using System.Linq;

namespace Oras.Remote
{
    internal static class ManifestUtil
    {
        internal static string[] DefaultManifestMediaTypes = new[]
        {
            DockerMediaTypes.Manifest,
            DockerMediaTypes.ManifestList,
            OCIMediaTypes.ImageIndex,
            OCIMediaTypes.ImageManifest
        };

        /// <summary>
        /// isManifest determines if the given descriptor is a manifest.
        /// </summary>
        /// <param name="manifestMediaTypes"></param>
        /// <param name="desc"></param>
        /// <returns></returns>
        public static bool IsManifest(string[] manifestMediaTypes, Descriptor desc)
        {
            if (manifestMediaTypes == null || manifestMediaTypes.Length == 0)
            {
                manifestMediaTypes = DefaultManifestMediaTypes;
            }

            if (manifestMediaTypes.Any((mediaType) => mediaType == desc.MediaType))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// ManifestAcceptHeader returns the accept header for the given manifest media types.
        /// </summary>
        /// <param name="manifestMediaTypes"></param>
        /// <returns></returns>
        public static string ManifestAcceptHeader(string[] manifestMediaTypes)
        {
            if (manifestMediaTypes == null || manifestMediaTypes.Length == 0)
            {
                manifestMediaTypes = DefaultManifestMediaTypes;
            }

            return string.Join(",", manifestMediaTypes);
        }
    }
}
