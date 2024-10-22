using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrasProject.Oras.Oci;

public class PackManifestOptions
{
    /// <summary>
    /// Config is references a configuration object for a container, by digest
    /// For more details: https://github.com/opencontainers/image-spec/blob/v1.1.0/manifest.md#:~:text=This%20REQUIRED%20property%20references,of%20the%20reference%20code.
    /// </summary>
    public Descriptor? Config { get; set; }

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

