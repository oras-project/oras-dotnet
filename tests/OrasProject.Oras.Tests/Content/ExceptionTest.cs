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

using OrasProject.Oras.Content.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Content;

public class ExceptionTest
{
    [Fact]
    public void InvalidDescriptorSizeException_Constructors()
    {
        var ex1 = new InvalidDescriptorSizeException();
        Assert.NotNull(ex1.Message);

        var ex2 = new InvalidDescriptorSizeException("Invalid descriptor size");
        Assert.Equal("Invalid descriptor size", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new InvalidDescriptorSizeException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void MismatchedDigestException_Constructors()
    {
        var ex1 = new MismatchedDigestException();
        Assert.NotNull(ex1.Message);

        var ex2 = new MismatchedDigestException("Mismatched digest");
        Assert.Equal("Mismatched digest", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new MismatchedDigestException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void MismatchedSizeException_Constructors()
    {
        var ex1 = new MismatchedSizeException();
        Assert.NotNull(ex1.Message);

        var ex2 = new MismatchedSizeException("Mismatched size");
        Assert.Equal("Mismatched size", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new MismatchedSizeException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void InvalidDigestException_Constructors()
    {
        var ex1 = new InvalidDigestException();
        Assert.NotNull(ex1.Message);

        var ex2 = new InvalidDigestException("Invalid digest");
        Assert.Equal("Invalid digest", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new InvalidDigestException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }
}
