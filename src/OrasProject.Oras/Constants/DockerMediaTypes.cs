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

namespace OrasProject.Oras.Constants
{
    public static class DockerMediaTypes
    {
        // Docker media types
        public const string Config = "application/vnd.docker.container.image.v1+json";
        public const string ManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
        public const string Manifest = "application/vnd.docker.distribution.manifest.v2+json";
        public const string ForeignLayer = "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip";
    }
}
