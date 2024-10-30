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

using OrasProject.Oras.Oci;

using System.Collections.Generic;
using System.Threading;

namespace OrasProject.Oras;

public struct PackManifestOptions
{
    public static PackManifestOptions None { get; }

    /// <summary>
    /// Config is references a configuration object for a container, by digest
    /// For more details: https://github.com/opencontainers/image-spec/blob/v1.1.0/manifest.md#:~:text=This%20REQUIRED%20property%20references,of%20the%20reference%20code.
    /// </summary>
    public Descriptor Config { get; set; }

    /// <summary>
    /// Layers is the layers of the manifest
    /// For more details: https://github.com/opencontainers/image-spec/blob/v1.1.0/manifest.md#:~:text=Each%20item%20in,the%20layers.
    /// </summary>
    public IList<Descriptor>? Layers { get; set; }

    /// <summary>
    /// Subject is the subject of the manifest.
    /// This option is only valid when PackManifestVersion is
    /// NOT PackManifestVersion1_0.
    /// </summary>
    public Descriptor? Subject { get; set; }

    /// <summary>
    /// ManifestAnnotations is OPTIONAL property contains arbitrary metadata for the image manifest
    // MUST use the annotation rules
    /// </summary>
    public IDictionary<string, string>? ManifestAnnotations { get; set; }

    /// <summary>
    /// ConfigAnnotations is the annotation map of the config descriptor.
    // This option is valid only when Config is null.
    /// </summary>
    public IDictionary<string, string>? ConfigAnnotations { get; set; }
}

