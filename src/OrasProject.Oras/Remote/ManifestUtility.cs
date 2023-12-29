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

using OrasProject.Oras.Constants;
using OrasProject.Oras.Oci;
using System.Linq;

namespace OrasProject.Oras.Remote
{
    internal static class ManifestUtility
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
        internal static bool IsManifest(string[] manifestMediaTypes, Descriptor desc)
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
        internal static string ManifestAcceptHeader(string[] manifestMediaTypes)
        {
            if (manifestMediaTypes == null || manifestMediaTypes.Length == 0)
            {
                manifestMediaTypes = DefaultManifestMediaTypes;
            }

            return string.Join(',', manifestMediaTypes);
        }
    }
}
