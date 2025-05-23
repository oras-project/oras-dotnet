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

using Xunit;

namespace OrasProject.Oras.Tests;

public class CopyGraphOptionsTest
{
    [Fact]
    public void DefaultMaxConcurrency_ShouldBeTen()
    {
        // Arrange & Act
        var options = new CopyGraphOptions();
        
        // Assert
        Assert.Equal(10, options.MaxConcurrency);
        options.Dispose();
    }

    [Fact]
    public void SemaphoreSlim_IsInitializedWithInitialCountOneAndMaxCountEqualToMaxConcurrency()
    {
        // Arrange
        var options = new CopyGraphOptions();

        // Act
        var semaphore = options.SemaphoreSlim;

        // Assert
        Assert.Equal(1, semaphore.CurrentCount);
        // The maximum count is stored internally; try to release up to MaxConcurrency - 1 more times
        for (int i = 0; i < options.MaxConcurrency - 1; i++)
        {
            semaphore.Release();
        }
        // After releasing MaxConcurrency times in total, the semaphore should be full
        Assert.Throws<SemaphoreFullException>(() => semaphore.Release());
        options.Dispose();
    }

    [Fact]
    public void Dispose_ShouldDisposeSemaphore()
    {
        // Arrange
        var options = new CopyGraphOptions();
        var semaphore = options.SemaphoreSlim;

        // Act
        options.Dispose();

        // Assert that the semaphore has been disposed
        Assert.Throws<ObjectDisposedException>(() => semaphore.Wait());
    }
}
