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
