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

namespace OrasProject.Oras.Oci;

/// <summary>
/// Standard OCI annotation keys.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.1/annotations.md
/// </summary>
public static class Annotations
{
    /// <summary>
    /// Created is the annotation key for the date and time on which the
    /// image was built (date-time string as defined by RFC 3339).
    /// </summary>
    public const string Created = "org.opencontainers.image.created";

    /// <summary>
    /// Authors is the annotation key for the contact details of the
    /// people or organization responsible for the image (freeform string).
    /// </summary>
    public const string Authors = "org.opencontainers.image.authors";

    /// <summary>
    /// Url is the annotation key for the URL to find more information
    /// on the image.
    /// </summary>
    public const string Url = "org.opencontainers.image.url";

    /// <summary>
    /// Documentation is the annotation key for the URL to get
    /// documentation on the image.
    /// </summary>
    public const string Documentation = "org.opencontainers.image.documentation";

    /// <summary>
    /// Source is the annotation key for the URL to get source code for
    /// building the image.
    /// </summary>
    public const string Source = "org.opencontainers.image.source";

    /// <summary>
    /// Version is the annotation key for the version of the packaged
    /// software.
    /// </summary>
    public const string Version = "org.opencontainers.image.version";

    /// <summary>
    /// Revision is the annotation key for the source control revision
    /// identifier for the packaged software.
    /// </summary>
    public const string Revision = "org.opencontainers.image.revision";

    /// <summary>
    /// Vendor is the annotation key for the name of the distributing
    /// entity, organization or individual.
    /// </summary>
    public const string Vendor = "org.opencontainers.image.vendor";

    /// <summary>
    /// Licenses is the annotation key for the license(s) under which
    /// contained software is distributed as an SPDX License Expression.
    /// </summary>
    public const string Licenses = "org.opencontainers.image.licenses";

    /// <summary>
    /// RefName is the annotation key for the name of the reference
    /// for a target.
    /// </summary>
    public const string RefName = "org.opencontainers.image.ref.name";

    /// <summary>
    /// Title is the annotation key for the human-readable title of
    /// the image.
    /// </summary>
    public const string Title = "org.opencontainers.image.title";

    /// <summary>
    /// Description is the annotation key for the human-readable
    /// description of the software packaged in the image.
    /// </summary>
    public const string Description = "org.opencontainers.image.description";

    /// <summary>
    /// BaseImageDigest is the annotation key for the digest of the
    /// image's base image.
    /// </summary>
    public const string BaseImageDigest = "org.opencontainers.image.base.digest";

    /// <summary>
    /// BaseImageName is the annotation key for the image reference of
    /// the image's base image.
    /// </summary>
    public const string BaseImageName = "org.opencontainers.image.base.name";
}
