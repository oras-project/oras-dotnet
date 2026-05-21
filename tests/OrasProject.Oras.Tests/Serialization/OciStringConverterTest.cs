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

using System.Text;
using OrasProject.Oras.Serialization;
using Xunit;

namespace OrasProject.Oras.Tests.Serialization;

public class OciStringConverterTest
{
    private static string Serialize(string value)
    {
        var bytes = OciJsonSerializer.SerializeToUtf8Bytes(value);
        return Encoding.UTF8.GetString(bytes);
    }

    [Theory]
    [InlineData("application/vnd.oci.image.manifest.v1+json",
        "+", "\\u002B")]
    [InlineData("<script>&foo</script>",
        "\\u003c", "<")]
    [InlineData("line\u2028para\u2029end",
        "\\u2028", null)]
    [InlineData("a\u0001z\u001Fc",
        "\\u0001", null)]
    [InlineData("\t\n\r\b\f",
        "\\t", null)]
    [InlineData("\b", "\\b", null)]
    [InlineData("\f", "\\f", null)]
    [InlineData("say \"hello\" \\ world",
        "\\\"", null)]
    public void Serialize_ShouldEscapeCorrectly(
        string input, string expected, string? forbidden)
    {
        var json = Serialize(input);
        Assert.Contains(expected, json);
        if (forbidden != null)
        {
            Assert.DoesNotContain(forbidden, json);
        }
    }

    [Theory]
    [InlineData("application/vnd.oci.image.manifest.v1+json")]
    [InlineData("application/vnd.oci.empty.v1+json")]
    [InlineData("with <html> & special + chars")]
    [InlineData("line\u2028para\u2029end")]
    public void RoundTrip_ShouldPreserveValue(string value)
    {
        var bytes = OciJsonSerializer.SerializeToUtf8Bytes(value);
        var result = OciJsonSerializer.Deserialize<string>(bytes);
        Assert.Equal(value, result);
    }
}
