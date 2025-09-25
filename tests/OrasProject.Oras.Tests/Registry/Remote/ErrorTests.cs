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

using System.Text.Json;
using OrasProject.Oras.Registry.Remote;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class ErrorTests
{
    [Fact]
    public void ToString_WithCodeAndMessage_FormatsCorrectly()
    {
        // Arrange
        var error = new Error
        {
            Code = "NAME_UNKNOWN",
            Message = "repository name not known to registry"
        };

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal("NAME_UNKNOWN: repository name not known to registry", result);
    }

    [Fact]
    public void ToString_WithNullCodeOrMessage_HandlesGracefully()
    {
        // Arrange
        var error = new Error
        {
            Code = null!,
            Message = "Some message"
        };

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal(": Some message", result);

        // Arrange again
        error = new Error
        {
            Code = "SOME_CODE",
            Message = null!
        };

        // Act again
        result = error.ToString();

        // Assert again
        Assert.Equal("SOME_CODE: ", result);
    }

    [Fact]
    public void ToString_WithDetailObject_IncludesDetail()
    {
        // Arrange
        var detailObject = JsonDocument.Parse(@"{""key"": ""value""}").RootElement;
        var error = new Error
        {
            Code = "DETAIL_ERROR",
            Message = "Error with detail",
            Detail = detailObject
        };

        // Act
        var result = error.ToString();

        // Assert
        Assert.Contains("DETAIL_ERROR: Error with detail", result);
        Assert.Contains("(Detail:", result);
        Assert.Contains("\"key\":\"value\"", result);
    }

    [Fact]
    public void ToString_WithInvalidDetail_SkipsDetailFormatting()
    {
        // Arrange - Create an error with a detail element that would cause issues with serialization
        var error = new Error
        {
            Code = "DETAIL_ERROR",
            Message = "Error with problematic detail",
            Detail = default // Null detail should be handled safely
        };

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal("DETAIL_ERROR: Error with problematic detail", result);
        Assert.DoesNotContain("(Detail:", result);
    }

    [Fact]
    public void ToString_WithNullOrUndefinedDetail_SkipsDetailFormatting()
    {
        // Arrange with Null JSON
        var nullJson = JsonDocument.Parse("null").RootElement;
        var error = new Error
        {
            Code = "DETAIL_ERROR",
            Message = "Error with null detail",
            Detail = nullJson
        };

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal("DETAIL_ERROR: Error with null detail", result);
        Assert.DoesNotContain("(Detail:", result);
    }
}
