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

using System;
using OrasProject.Oras.Content.File.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File.Exceptions;

public class ExceptionTest
{
    [Fact]
    public void FileStoreClosedException_Constructors()
    {
        var ex1 = new FileStoreClosedException();
        Assert.Equal("Store already closed", ex1.Message);

        var ex2 = new FileStoreClosedException("custom");
        Assert.Equal("custom", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new FileStoreClosedException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void DuplicateFileNameException_Constructors()
    {
        var ex1 = new DuplicateFileNameException();
        Assert.NotNull(ex1.Message);

        var ex2 = new DuplicateFileNameException("Duplicate name");
        Assert.Equal("Duplicate name", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new DuplicateFileNameException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void MissingNameException_Constructors()
    {
        var ex1 = new MissingNameException();
        Assert.NotNull(ex1.Message);

        var ex2 = new MissingNameException("Missing name");
        Assert.Equal("Missing name", ex2.Message);

        var ex3 = new MissingNameException("Missing name", "myParam");
        Assert.Contains("Missing name", ex3.Message);
        Assert.Equal("myParam", ex3.ParamName);

        var inner = new InvalidOperationException("inner");
        var ex4 = new MissingNameException("msg", inner);
        Assert.Equal("msg", ex4.Message);
        Assert.Same(inner, ex4.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_Constructors()
    {
        var ex1 = new OverwriteDisallowedException();
        Assert.NotNull(ex1.Message);

        var ex2 = new OverwriteDisallowedException("Overwrite disallowed");
        Assert.Equal("Overwrite disallowed", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new OverwriteDisallowedException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_Constructors()
    {
        var ex1 = new PathTraversalDisallowedException();
        Assert.NotNull(ex1.Message);

        var ex2 = new PathTraversalDisallowedException(
            "Path traversal disallowed");
        Assert.Equal("Path traversal disallowed", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new PathTraversalDisallowedException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }
}
