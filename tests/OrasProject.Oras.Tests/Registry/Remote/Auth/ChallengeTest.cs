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

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

public class ChallengeTest
{
    [Theory]
    [InlineData("Basic realm=\"example\"", ChallengeScheme.Basic, null)]
    [InlineData("Bearer realm=\"example\",service=\"example.com\"", ChallengeScheme.Bearer, new[] { "realm", "example", "service", "example.com" })]
    [InlineData("Bearer realm=\"example\",service=\"example.com\",key1=value1,key2=value2", ChallengeScheme.Bearer, new[] { "realm", "example", "service", "example.com", "key1", "value1", "key2", "value2" })]
    [InlineData(
        "Bearer realm=\"https://abc.io/oauth2/token\",service=\"abc.io\",scope=\"repository:nginx:push,pull\",error=\"insufficient_scope\"",
        ChallengeScheme.Bearer,
        new[] { "realm", "https://abc.io/oauth2/token", "service", "abc.io", "scope", "repository:nginx:push,pull", "error", "insufficient_scope" })]
    [InlineData(
        "bearer realm=\"https://abc.io/oauth2/token\"  ,   service=\"abc.io\",  scope=\"repository:nginx:push,pull\"   ,  key1=value1  ,  key2=value2  ",
        ChallengeScheme.Bearer,
        new[] { "realm", "https://abc.io/oauth2/token", "service", "abc.io", "scope", "repository:nginx:push,pull", "key1", "value1", "key2", "value2" })]
    [InlineData(
        "BEARER realm=\"https://registry.io/oauth2/token\",service=\"registry.io\",scope=\"repository:nginx:push,pull repository:abc:delete\",error=\"insufficient_scope\"",
        ChallengeScheme.Bearer,
        new[] { "realm", "https://registry.io/oauth2/token", "service", "registry.io", "scope", "repository:nginx:push,pull repository:abc:delete", "error", "insufficient_scope" })]

    [InlineData("Unknown realm=\"example\"", ChallengeScheme.Unknown, null)]
    [InlineData(null, ChallengeScheme.Unknown, null)]
    public void ParseChallenge_ValidHeader_ReturnsExpectedSchemeAndParams(string? header, ChallengeScheme expectedScheme, string[]? expectedParams)
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
    public void ParseChallenge_DuplicateParameterKey_LastValueWins()
    {
        var header =
            "BEARER realm=\"https://registry.io/oauth2/token\",service=\"first.io\",service=\"second.io\",scope=\"repository:nginx:push,pull\"";
        var (scheme, parameters) = Challenge.ParseChallenge(header);

        Assert.Equal(ChallengeScheme.Bearer, scheme);
        Assert.NotNull(parameters);
        // A repeated parameter key keeps its last value rather than rejecting the whole challenge.
        Assert.Equal("second.io", parameters["service"]);
    }

    [Fact]
    public void ParseChallenge_UnterminatedQuotedValue_ThrowsFormatException()
    {
        var header = "Bearer realm=\"https://registry.io/oauth2/token";
        Assert.Throws<FormatException>(() => Challenge.ParseChallenge(header));
    }

    [Fact]
    public void TryParseChallenge_UnterminatedQuotedValue_ReturnsFalse()
    {
        var header = "Bearer realm=\"https://registry.io/oauth2/token";
        var result = Challenge.TryParseChallenge(header, out var challenge);

        Assert.False(result);
        Assert.Null(challenge.Parameters);
    }

    [Fact]
    public void TryParseChallenge_ValidHeader_ReturnsTrueWithParsedParameters()
    {
        var header = "Bearer realm=\"https://registry.io/oauth2/token\",service=\"registry.io\"";
        var result = Challenge.TryParseChallenge(header, out var challenge);

        Assert.True(result);
        Assert.Equal(ChallengeScheme.Bearer, challenge.Scheme);
        Assert.NotNull(challenge.Parameters);
        Assert.Equal("https://registry.io/oauth2/token", challenge.Parameters["realm"]);
        Assert.Equal("registry.io", challenge.Parameters["service"]);
    }

    [Theory]
    [InlineData("Basic", ChallengeScheme.Basic)]
    [InlineData("Bearer", ChallengeScheme.Bearer)]
    [InlineData("BASIC", ChallengeScheme.Basic)]
    [InlineData("BEARER", ChallengeScheme.Bearer)]
    [InlineData("basic", ChallengeScheme.Basic)]
    [InlineData("bearer", ChallengeScheme.Bearer)]
    [InlineData("Unknown", ChallengeScheme.Unknown)]
    [InlineData("Basic abd", ChallengeScheme.Unknown)]
    public void ParseScheme_ValidSchemeString_ReturnsExpectedScheme(string schemeString, ChallengeScheme expectedScheme)
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
    public void IsValidTokenChar_ShouldReturnExpectedResult(char input, bool expected)
    {
        // Act
        var result = Challenge.IsValidTokenChar(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
