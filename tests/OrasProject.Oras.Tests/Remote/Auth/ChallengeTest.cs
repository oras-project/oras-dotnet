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

using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Remote.Auth;

public class ChallengeTest
{
    [Theory]
    [InlineData("Basic realm=\"example\"", Challenge.Scheme.Basic, null)]
    [InlineData("Bearer realm=\"example\",service=\"example.com\"", Challenge.Scheme.Bearer, new[] { "realm", "example", "service", "example.com" })]
    [InlineData("Bearer realm=\"example\",service=\"example.com\",key1=value1,key2=value2", Challenge.Scheme.Bearer, new[] { "realm", "example", "service", "example.com", "key1", "value1", "key2", "value2" })]
    [InlineData(
        "Bearer realm=\"https://abc.io/oauth2/token\",service=\"abc.io\",scope=\"repository:nginx:push,pull\",error=\"insufficient_scope\"", 
        Challenge.Scheme.Bearer, 
        new[] { "realm", "https://abc.io/oauth2/token", "service", "abc.io", "scope", "repository:nginx:push,pull", "error", "insufficient_scope" })]
    [InlineData(
        "bearer realm=\"https://abc.io/oauth2/token\"  ,   service=\"abc.io\",  scope=\"repository:nginx:push,pull\"   ,  key1=value1  ,  key2=value2  ", 
        Challenge.Scheme.Bearer, 
        new[] { "realm", "https://abc.io/oauth2/token", "service", "abc.io", "scope", "repository:nginx:push,pull", "key1", "value1",  "key2", "value2" })]
    [InlineData(
        "BEARER realm=\"https://registry.io/oauth2/token\",service=\"registry.io\",scope=\"repository:nginx:push,pull repository:abc:delete\",error=\"insufficient_scope\"", 
        Challenge.Scheme.Bearer, 
        new[] { "realm", "https://registry.io/oauth2/token", "service", "registry.io", "scope", "repository:nginx:push,pull repository:abc:delete", "error", "insufficient_scope" })]

    [InlineData("Unknown realm=\"example\"", Challenge.Scheme.Unknown, null)]
    public void ParseChallenge_ValidHeader_ReturnsExpectedSchemeAndParams(string header, Challenge.Scheme expectedScheme, string[]? expectedParams)
    {
        var (scheme, parameters) = Challenge.ParseChallenge(header);

        Assert.Equal(expectedScheme, scheme);

        if (expectedParams == null)
        {
            Assert.Null(parameters);
        }
        else
        {
            Assert.NotNull(parameters);
            for (int i = 0; i < expectedParams.Length; i += 2)
            {
                Assert.Equal(expectedParams[i + 1], parameters[expectedParams[i]]);
            }
        }
    }
    
    [Fact]
    public void ParseChallenge_ThrowsArgumentException()
    {
        var header =
            "BEARER realm=\"https://registry.io/oauth2/token\",service=\"registry.io\",service=\"registry.io\",scope=\"repository:nginx:push,pull repository:abc:delete\",error=\"insufficient_scope\"";
        var message = Assert.Throws<ArgumentException>(() => Challenge.ParseChallenge(header));
        Assert.Equal("An item with the same key has already been added. Key: service", message.Message);
    }

    [Theory]
    [InlineData("Basic", Challenge.Scheme.Basic)]
    [InlineData("Bearer", Challenge.Scheme.Bearer)]
    [InlineData("BASIC", Challenge.Scheme.Basic)]
    [InlineData("BEARER", Challenge.Scheme.Bearer)]
    [InlineData("basic", Challenge.Scheme.Basic)]
    [InlineData("bearer", Challenge.Scheme.Bearer)]
    [InlineData("Unknown", Challenge.Scheme.Unknown)]
    [InlineData("Basic abd", Challenge.Scheme.Unknown)]
    public void ParseScheme_ValidSchemeString_ReturnsExpectedScheme(string schemeString, Challenge.Scheme expectedScheme)
    {
        var scheme = Challenge.ParseScheme(schemeString);
        Assert.Equal(expectedScheme, scheme);
    }

    [Theory]
    [InlineData("token123", "token123", "")]
    [InlineData("token123 rest", "token123", " rest")]
    [InlineData("token123, rest", "token123", ", rest")]
    [InlineData("token123:rest", "token123", ":rest")]
    public void ParseToken_ValidToken_ReturnsExpectedParts(string token, string expectedToken, string expectedRest)
    {
        var (parsedToken, rest) = Challenge.ParseToken(token);
        Assert.Equal(expectedToken, parsedToken);
        Assert.Equal(expectedRest, rest);
    }
    
    [Theory]
    [InlineData('A', true)] // Uppercase letter
    [InlineData('z', true)] // Lowercase letter
    [InlineData('5', true)] // Digit
    [InlineData('!', true)] // Special character in the list
    [InlineData(' ', false)]  // Space (not a valid token character)
    [InlineData(',', false)]  // comma (not a valid token character)
    [InlineData(':', false)]  // colon (not a valid token character)
    [InlineData('@', false)]  // Special character not in the list
    [InlineData('\n', false)] // Newline (not a valid token character)
    public void IsTokenChar_ShouldReturnExpectedResult(char input, bool expected)
    {
        // Act
        var result = Challenge.IsValidTokenChar(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
