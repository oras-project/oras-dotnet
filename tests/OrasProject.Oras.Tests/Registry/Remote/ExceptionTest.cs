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

using OrasProject.Oras.Registry.Remote.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class ExceptionTest
{
    [Fact]
    public void ReferrersStateAlreadySetException_Constructors()
    {
        var ex1 = new ReferrersStateAlreadySetException();
        Assert.NotNull(ex1.Message);

        var ex2 = new ReferrersStateAlreadySetException(
            "Referrers state has already been set");
        Assert.Equal(
            "Referrers state has already been set",
            ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new ReferrersStateAlreadySetException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void InvalidResponseException_Constructors()
    {
        var ex1 = new InvalidResponseException();
        Assert.NotNull(ex1.Message);

        var ex2 = new InvalidResponseException("Invalid response");
        Assert.Equal("Invalid response", ex2.Message);

        var inner = new InvalidOperationException("inner");
        var ex3 = new InvalidResponseException("msg", inner);
        Assert.Equal("msg", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }
}
