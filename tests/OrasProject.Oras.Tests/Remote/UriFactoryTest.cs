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

using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using Xunit;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;

namespace OrasProject.Oras.Tests.Remote;

public class UriFactoryTest
{
    [Fact]
    public void BuildReferrersUrl_WithArtifactType_ShouldAddArtifactTypeToQueryString()
    {
        var desc = RandomDescriptor();

        var reference = Reference.Parse("localhost:5000/test");
        reference.ContentReference = desc.Digest;

        const string artifactType = "doc/example";
        var expectedPath = $"referrers/{reference.ContentReference}";
        const string expectedQuery = "artifactType=doc%2fexample";
        var result = new UriFactory(reference).BuildReferrersUrl(artifactType);
        Assert.Equal($"https://localhost:5000/v2/test/{expectedPath}?{expectedQuery}", result.ToString());
    }    

    [Fact]
    public void BuildReferrersUrl_WithoutArtifactType()
    {
        var desc = RandomDescriptor();
        var reference = Reference.Parse("localhost:5000/test");
        reference.ContentReference = desc.Digest;


        var expectedPath = $"referrers/{reference.ContentReference}";
        var result = new UriFactory(reference).BuildReferrersUrl();
        Assert.Equal($"https://localhost:5000/v2/test/{expectedPath}", result.ToString());
    }   
}
