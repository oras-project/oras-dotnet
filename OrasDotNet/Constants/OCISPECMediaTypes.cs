using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotnet.Constants
{
    internal class OCISPECMediaTypes
    {
        // MediaTypeDescriptor specifies the media type for a content descriptor.
        public const string Descriptor = "application/vnd.oci.descriptor.v1+json";

        // MediaTypeLayoutHeader specifies the media type for the oci-layout.
        public const string LayoutHeader = "application/vnd.oci.layout.header.v1+json";

        // MediaTypeImageManifest specifies the media type for an image manifest.
        public const string ImageManifest = "application/vnd.oci.image.manifest.v1+json";

        // MediaTypeImageIndex specifies the media type for an image index.
        public const string ImageIndex = "application/vnd.oci.image.index.v1+json";

        // MediaTypeImageLayer is the media type used for layers referenced by the manifest.
        public const string ImageLayer = "application/vnd.oci.image.layer.v1.tar";

        // MediaTypeImageLayerGzip is the media type used for gzipped layers
        // referenced by the manifest.
        public const string ImageLayerGzip = "application/vnd.oci.image.layer.v1.tar+gzip";

        // MediaTypeImageLayerZstd is the media type used for zstd compressed
        // layers referenced by the manifest.
        public const string ImageLayerZstd = "application/vnd.oci.image.layer.v1.tar+zstd";

        // MediaTypeImageLayerNonDistributable is the media type for layers referenced by
        // the manifest but with distribution restrictions.
        public const string ImageLayerNonDistributable = "application/vnd.oci.image.layer.nondistributable.v1.tar";

        // MediaTypeImageLayerNonDistributableGzip is the media type for
        // gzipped layers referenced by the manifest but with distribution
        // restrictions.
        public const string ImageLayerNonDistributableGzip = "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip";

        // MediaTypeImageLayerNonDistributableZstd is the media type for zstd
        // compressed layers referenced by the manifest but with distribution
        // restrictions.
        public const string ImageLayerNonDistributableZstd = "application/vnd.oci.image.layer.nondistributable.v1.tar+zstd";

        // MediaTypeImageConfig specifies the media type for the image configuration.
        public const string ImageConfig = "application/vnd.oci.image.config.v1+json";

        // MediaTypeArtifactManifest specifies the media type for a content descriptor.
        public const string ArtifactManifest = "application/vnd.oci.artifact.manifest.v1+json";
    }
}
