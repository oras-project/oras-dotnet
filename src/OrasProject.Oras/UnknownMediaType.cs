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

namespace OrasProject.Oras;

public static class UnknownMediaType
{
    /// <summary>
    /// MediaTypeUnknownConfig is the default config mediaType used
    /// - for [Pack] when PackOptions.PackImageManifest is true and
    ///   PackOptions.ConfigDescriptor is not specified.
    /// - for [PackManifest] when packManifestVersion is PackManifestVersion1_0
    ///   and PackManifestOptions.ConfigDescriptor is not specified.
    /// </summary>
    public const string UnknownConfig = "application/vnd.unknown.config.v1+json";

    /// <summary>
    /// MediaTypeUnknownArtifact is the default artifactType used for [Pack]
    /// when PackOptions.PackImageManifest is false and artifactType is
    /// not specified.
    /// </summary>
    public const string UnknownArtifact = "application/vnd.unknown.artifact.v1";
}
