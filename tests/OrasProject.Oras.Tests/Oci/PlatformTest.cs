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
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests.Oci
{
    public class PlatformTest
    {
        [Fact]
        public void PlatformSerialization()
        {
            var platform = new Platform
            {
                Architecture = "amd64",
                Os = "linux",
                OsVersion = "5.4.0-42-generic",
                OsFeatures = ["feature1", "feature2"],
                Variant = "v8"
            };
            var json = JsonSerializer.Serialize(platform);
            Assert.Contains("\"architecture\":\"amd64\"", json);
            Assert.Contains("\"os\":\"linux\"", json);
            Assert.Contains("\"os.version\":\"5.4.0-42-generic\"", json);
            Assert.Contains("\"os.features\":[\"feature1\",\"feature2\"]", json);
            Assert.Contains("\"variant\":\"v8\"", json);
        }

        [Fact]
        public void Serialization_OmitsDefaultFields()
        {
            var platform = new Platform
            {
                Architecture = "amd64",
                Os = "linux"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(platform);
            Assert.Contains("\"architecture\":\"amd64\"", json);
            Assert.Contains("\"os\":\"linux\"", json);
            Assert.DoesNotContain("\"os.version\"", json);
            Assert.DoesNotContain("\"os.features\"", json);
            Assert.DoesNotContain("\"variant\"", json);
        }
    }
}
