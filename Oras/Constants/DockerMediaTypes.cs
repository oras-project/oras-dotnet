﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Constants
{
    internal class DockerMediaTypes
    {
        // Docker media types
       public const string Config = "application/vnd.docker.container.image.v1+json";
       public const string ManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
       public const string Manifest = "application/vnd.docker.distribution.manifest.v2+json";
       public const string ForeignLayer = "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip";
    }
}
