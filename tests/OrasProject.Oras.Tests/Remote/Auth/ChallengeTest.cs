using Xunit;

namespace OrasProject.Oras.Tests.Remote.Auth;

public class ChallengeTest
{
    [Theory]
    [InlineData('A', false)] // Uppercase letter
    [InlineData('z', false)] // Lowercase letter
    [InlineData('5', false)] // Digit
    [InlineData('!', false)] // Special character in the list
    [InlineData(' ', true)]  // Space (not a valid token character)
    [InlineData('@', true)]  // Special character not in the list
    [InlineData('\n', true)] // Newline (not a valid token character)
    public void IsNotTokenChar_ShouldReturnExpectedResult(char input, bool expected)
    {
        // Act
        var result = InvokeIsNotTokenChar(input);

        // Assert
        Assert.Equal(expected, result);
    }

    private static bool InvokeIsNotTokenChar(char c)
    {
        // Use reflection to access the private method
        var method = typeof(OrasProject.Oras.Registry.Remote.Auth.Challenge)
            .GetMethod("IsNotTokenChar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool)method.Invoke(null, new object[] { c });
    }
}
