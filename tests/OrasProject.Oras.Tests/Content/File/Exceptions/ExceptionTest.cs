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

using OrasProject.Oras.Content.File.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File.Exceptions;

public class ExceptionTest
{
    [Fact]
    public void FileStoreClosedException_DefaultConstructor()
    {
        var ex = new FileStoreClosedException();
        Assert.Equal("Store already closed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void FileStoreClosedException_MessageConstructor()
    {
        var ex = new FileStoreClosedException("Custom message");
        Assert.Equal("Custom message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void FileStoreClosedException_NullMessageConstructor()
    {
        var ex = new FileStoreClosedException(null);
        Assert.Equal("Store already closed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void FileStoreClosedException_MessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new FileStoreClosedException("Custom message", inner);
        Assert.Equal("Custom message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void FileStoreClosedException_NullMessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new FileStoreClosedException(null, inner);
        Assert.Equal("Store already closed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void DuplicateNameException_DefaultConstructor()
    {
        var ex = new DuplicateNameException();
        Assert.Equal("Duplicate name", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void DuplicateNameException_MessageConstructor()
    {
        var ex = new DuplicateNameException("Custom message");
        Assert.Equal("Custom message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void DuplicateNameException_NullMessageConstructor()
    {
        var ex = new DuplicateNameException(null);
        Assert.Equal("Duplicate name", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void DuplicateNameException_MessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new DuplicateNameException("Custom message", inner);
        Assert.Equal("Custom message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void DuplicateNameException_NullMessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new DuplicateNameException(null, inner);
        Assert.Equal("Duplicate name", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_DefaultConstructor()
    {
        var ex = new PathTraversalDisallowedException();
        Assert.Equal("Path traversal disallowed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_MessageConstructor()
    {
        var ex = new PathTraversalDisallowedException("Custom message");
        Assert.Equal("Custom message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_NullMessageConstructor()
    {
        var ex = new PathTraversalDisallowedException(null);
        Assert.Equal("Path traversal disallowed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_MessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new PathTraversalDisallowedException("Custom message", inner);
        Assert.Equal("Custom message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void PathTraversalDisallowedException_NullMessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new PathTraversalDisallowedException(null, inner);
        Assert.Equal("Path traversal disallowed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_DefaultConstructor()
    {
        var ex = new OverwriteDisallowedException();
        Assert.Equal("Overwrite disallowed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_MessageConstructor()
    {
        var ex = new OverwriteDisallowedException("Custom message");
        Assert.Equal("Custom message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_NullMessageConstructor()
    {
        var ex = new OverwriteDisallowedException(null);
        Assert.Equal("Overwrite disallowed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_MessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new OverwriteDisallowedException("Custom message", inner);
        Assert.Equal("Custom message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void OverwriteDisallowedException_NullMessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new OverwriteDisallowedException(null, inner);
        Assert.Equal("Overwrite disallowed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void MissingNameException_DefaultConstructor()
    {
        var ex = new MissingNameException();
        Assert.Equal("Missing name", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void MissingNameException_MessageConstructor()
    {
        var ex = new MissingNameException("Custom message");
        Assert.Equal("Custom message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void MissingNameException_NullMessageConstructor()
    {
        var ex = new MissingNameException(null);
        Assert.Equal("Missing name", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void MissingNameException_MessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new MissingNameException("Custom message", inner);
        Assert.Equal("Custom message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void MissingNameException_NullMessageAndInnerConstructor()
    {
        var inner = new InvalidOperationException("Inner exception");
        var ex = new MissingNameException(null, inner);
        Assert.Equal("Missing name", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
