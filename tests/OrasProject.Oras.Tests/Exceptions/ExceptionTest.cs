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

using OrasProject.Oras.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Exceptions;

public class ExceptionTest
{
    [Fact]
    public void AlreadyExistsException_Constructors()
    {
        var ex1 = new AlreadyExistsException();
        Assert.NotNull(ex1.Message);

        var ex2 = new AlreadyExistsException("Already exists");
        Assert.Equal("Already exists", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new AlreadyExistsException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void NotFoundException_Constructors()
    {
        var ex1 = new NotFoundException();
        Assert.NotNull(ex1.Message);

        var ex2 = new NotFoundException("Not found");
        Assert.Equal("Not found", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new NotFoundException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void SizeLimitExceededException_Constructors()
    {
        var ex1 = new SizeLimitExceededException();
        Assert.NotNull(ex1.Message);

        var ex2 = new SizeLimitExceededException("Size limit exceeded");
        Assert.Equal("Size limit exceeded", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new SizeLimitExceededException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void MissingArtifactTypeException_Constructors()
    {
        var ex1 = new MissingArtifactTypeException();
        Assert.NotNull(ex1.Message);

        var ex2 = new MissingArtifactTypeException("Missing artifact type");
        Assert.Equal("Missing artifact type", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new MissingArtifactTypeException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void InvalidMediaTypeException_Constructors()
    {
        var ex1 = new InvalidMediaTypeException();
        Assert.NotNull(ex1.Message);

        var ex2 = new InvalidMediaTypeException("Invalid media type");
        Assert.Equal("Invalid media type", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new InvalidMediaTypeException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void InvalidDateTimeFormatException_Constructors()
    {
        var ex1 = new InvalidDateTimeFormatException();
        Assert.NotNull(ex1.Message);

        var ex2 = new InvalidDateTimeFormatException(
            "Invalid date time format");
        Assert.Equal("Invalid date time format", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new InvalidDateTimeFormatException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }
}
