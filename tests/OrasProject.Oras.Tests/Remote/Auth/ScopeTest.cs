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

namespace OrasProject.Oras.Tests.Remote.Auth;

public class ScopeTest
{
    [Fact]
    public void TryParse_ValidScopeString_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string scopeStr = "repository:my-repo:pull,push";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.True(result);
        Assert.NotNull(scope);
        Assert.Equal("repository", scope!.ResourceType);
        Assert.Equal("my-repo", scope.ResourceName);
        Assert.Contains(Scope.Action.Pull, scope.Actions);
        Assert.Contains(Scope.Action.Push, scope.Actions);
        Assert.Equal(2, scope.Actions.Count);
        Assert.DoesNotContain(Scope.Action.Delete, scope.Actions);
    }
    
    [Fact]
    public void TryParse_ValidScopeStringWithWildCard_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string scopeStr = "repository:my-repo:pull,push,delete,*";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.True(result);
        Assert.NotNull(scope);
        Assert.Equal("repository", scope!.ResourceType);
        Assert.Equal("my-repo", scope.ResourceName);
        Assert.Contains(Scope.Action.All, scope.Actions);
        Assert.Single(scope.Actions);
    }

    [Fact]
    public void TryParse_ValidScopeStringWithExtraWhiteSpaces_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string scopeStr = " repository : my-repo : pull, delete ,push";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.True(result);
        Assert.NotNull(scope);
        Assert.Equal("repository", scope!.ResourceType);
        Assert.Equal("my-repo", scope.ResourceName);
        Assert.Contains(Scope.Action.Pull, scope.Actions);
        Assert.Contains(Scope.Action.Delete, scope.Actions);
        Assert.Contains(Scope.Action.Push, scope.Actions);
        Assert.Equal(3, scope.Actions.Count);
    }

    [Fact]
    public void TryParse_InvalidScopeString_ReturnsFalse()
    {
        // Arrange
        string scopeStr = "invalid-scope-string";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.False(result);
        Assert.Null(scope);
    }

    [Fact]
    public void TryParse_EmptyScopeString_ReturnsFalse()
    {
        // Arrange
        string scopeStr = "";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.False(result);
        Assert.Null(scope);
    }

    [Fact]
    public void TryParse_ScopeStringWithInvalidAction_ReturnsFalse()
    {
        // Arrange
        string scopeStr = "repository:my-repo:pull,invalid-action";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.False(result);
        Assert.Null(scope);
    }

    [Fact]
    public void TryParse_ScopeStringWithExtraParts_ReturnsFalse()
    {
        // Arrange
        string scopeStr = "repository:my-repo:pull:extra-part";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.False(result);
        Assert.Null(scope);
    }

    [Fact]
    public void TryParse_ScopeStringWithMissingParts_ReturnsFalse()
    {
        // Arrange
        string scopeStr = "repository:my-repo";

        // Act
        bool result = Scope.TryParse(scopeStr, out var scope);

        // Assert
        Assert.False(result);
        Assert.Null(scope);
    }


    [Fact]
    public void TryParseAction_ValidActionString_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string actionStr = "pull";

        // Act
        bool result = Scope.TryParseAction(actionStr, out var action);

        // Assert
        Assert.True(result);
        Assert.NotNull(action);
        Assert.Equal(Scope.Action.Pull, action);
    }

    [Fact]
    public void TryParseAction_InvalidActionString_ReturnsFalse()
    {
        // Arrange
        string actionStr = "invalid-action";

        // Act
        bool result = Scope.TryParseAction(actionStr, out var action);

        // Assert
        Assert.False(result);
        Assert.Null(action);
    }

    [Fact]
    public void TryParseAction_ValidActionStringWithExtraWhiteSpaces_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string actionStr = " push ";

        // Act
        bool result = Scope.TryParseAction(actionStr.Trim(), out var action);

        // Assert
        Assert.True(result);
        Assert.NotNull(action);
        Assert.Equal(Scope.Action.Push, action);
    }
    
    [Fact]
    public void TryParseAction_ValidActionStringWithUpperCases_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string actionStr = " Push ";

        // Act
        bool result = Scope.TryParseAction(actionStr.Trim(), out var action);

        // Assert
        Assert.True(result);
        Assert.NotNull(action);
        Assert.Equal(Scope.Action.Push, action);
    }

    [Fact]
    public void TryParseAction_EmptyActionString_ReturnsFalse()
    {
        // Arrange
        string actionStr = "";

        // Act
        bool result = Scope.TryParseAction(actionStr, out var action);

        // Assert
        Assert.False(result);
        Assert.Null(action);
    }

    [Fact]
    public void TryParseAction_ValidDeleteActionString_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string actionStr = "delete";

        // Act
        bool result = Scope.TryParseAction(actionStr, out var action);

        // Assert
        Assert.True(result);
        Assert.NotNull(action);
        Assert.Equal(Scope.Action.Delete, action);
    }
    
    [Fact]
    public void TryParseAction_ValidWildCardActionString_ReturnsTrueAndParsesCorrectly()
    {
        // Arrange
        string actionStr = "*";

        // Act
        bool result = Scope.TryParseAction(actionStr, out var action);

        // Assert
        Assert.True(result);
        Assert.NotNull(action);
        Assert.Equal(Scope.Action.All, action);
    }


    // [Fact]
    // public void Equals_SameScopeInstances_ReturnsTrue()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //     var scope2 = scope1;
    //
    //     // Act
    //     bool result = scope1.Equals(scope1, scope2);
    //
    //     // Assert
    //     Assert.True(result);
    // }
    //
    // [Fact]
    // public void Equals_DifferentScopeInstancesWithSameValues_ReturnsTrue()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //     var scope2 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Delete });
    //
    //     // Act
    //     bool result = scope1.Equals(scope1, scope2);
    //
    //     // Assert
    //     Assert.True(result);
    // }
    
    [Fact]
    public void EqualsInSortedSet_DifferentScopeInstancesWithSameValues_ReturnsTrue()
    {
        // Arrange
        var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
        var scope2 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Delete });
        var scope3 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Push, Scope.Action.Pull });
        var scope4 = new Scope("repository", "my-repo1", new HashSet<Scope.Action> { Scope.Action.Push, Scope.Action.Pull });


        // Act
        var sortedSet = new SortedSet<Scope>
        {
            scope1,
            scope2,
            scope3,
            scope4
        };
        // Assert
        Assert.Equal(2, sortedSet.Count);
    }

    // [Fact]
    // public void Equals_DifferentScopeInstancesWithDifferentValues_ReturnsFalse()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //     var scope2 = new Scope("repository", "other-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //
    //     // Act
    //     bool result = scope1.Equals(scope1, scope2);
    //
    //     // Assert
    //     Assert.False(result);
    // }
    //
    // [Fact]
    // public void Equals_NullScopeInstances_ReturnsFalse()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //
    //     // Act
    //     bool result = scope1.Equals(scope1, null);
    //
    //     // Assert
    //     Assert.False(result);
    // }
    //
    // [Fact]
    // public void Equals_BothNullScopeInstances_ReturnsTrue()
    // {
    //     // Act
    //     bool result = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull }).Equals(null, null);
    //
    //     // Assert
    //     Assert.True(result);
    // }
    //
    // [Fact]
    // public void GetHashCode_SameScopeInstances_ReturnsSameHashCode()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //     var scope2 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //
    //     // Act
    //     int hash1 = scope1.GetHashCode(scope1);
    //     int hash2 = scope1.GetHashCode(scope2);
    //
    //     // Assert
    //     Assert.Equal(hash1, hash2);
    // }
    //
    // [Fact]
    // public void GetHashCode_DifferentScopeInstancesWithDifferentValues_ReturnsDifferentHashCodes()
    // {
    //     // Arrange
    //     var scope1 = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //     var scope2 = new Scope("repository", "other-repo", new HashSet<Scope.Action> { Scope.Action.Pull });
    //
    //     // Act
    //     int hash1 = scope1.GetHashCode(scope1);
    //     int hash2 = scope1.GetHashCode(scope2);
    //
    //     // Assert
    //     Assert.NotEqual(hash1, hash2);
    // }
    //
    // [Fact]
    // public void GetHashCode_NullScopeInstance_ReturnsZero()
    // {
    //     // Act
    //     int hash = new Scope("repository", "my-repo", new HashSet<Scope.Action> { Scope.Action.Pull }).GetHashCode(null);
    //
    //     // Assert
    //     Assert.Equal(0, hash);
    // }
}
