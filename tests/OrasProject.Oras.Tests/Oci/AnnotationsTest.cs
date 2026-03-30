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
using Xunit;

namespace OrasProject.Oras.Tests.Oci;

public class AnnotationsTest
{
    [Fact]
    public void OciAnnotations_HasCorrectValues()
    {
        Assert.Equal(
            "org.opencontainers.image.created",
            Annotations.Created);
        Assert.Equal(
            "org.opencontainers.image.authors",
            Annotations.Authors);
        Assert.Equal(
            "org.opencontainers.image.url",
            Annotations.Url);
        Assert.Equal(
            "org.opencontainers.image.documentation",
            Annotations.Documentation);
        Assert.Equal(
            "org.opencontainers.image.source",
            Annotations.Source);
        Assert.Equal(
            "org.opencontainers.image.version",
            Annotations.Version);
        Assert.Equal(
            "org.opencontainers.image.revision",
            Annotations.Revision);
        Assert.Equal(
            "org.opencontainers.image.vendor",
            Annotations.Vendor);
        Assert.Equal(
            "org.opencontainers.image.licenses",
            Annotations.Licenses);
        Assert.Equal(
            "org.opencontainers.image.ref.name",
            Annotations.RefName);
        Assert.Equal(
            "org.opencontainers.image.title",
            Annotations.Title);
        Assert.Equal(
            "org.opencontainers.image.description",
            Annotations.Description);
        Assert.Equal(
            "org.opencontainers.image.base.digest",
            Annotations.BaseImageDigest);
        Assert.Equal(
            "org.opencontainers.image.base.name",
            Annotations.BaseImageName);
    }
}
