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

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

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
        Assert.Contains(Scope.ActionPull, scope.Actions);
        Assert.Contains(Scope.ActionPush, scope.Actions);
        Assert.Equal(2, scope.Actions.Count);
        Assert.DoesNotContain(Scope.ActionDelete, scope.Actions);
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
        Assert.Contains(Scope.ActionWildcard, scope.Actions);
        Assert.Single(scope.Actions);
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
    public void EqualsInSortedSet_DifferentScopeInstancesWithSameValues_ReturnsTrue()
    {
        // Arrange
        var scope1 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });
        var scope2 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionDelete });
        var scope3 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPush, Scope.ActionPull });
        var scope4 = new Scope("repository", "my-repo1", new HashSet<string> { Scope.ActionPush, Scope.ActionPull });


        // Act
        var sortedSet = new SortedSet<Scope>();
        Scope.AddOrMergeScope(sortedSet, scope1);
        Scope.AddOrMergeScope(sortedSet, scope2);
        Scope.AddOrMergeScope(sortedSet, scope3);
        Scope.AddOrMergeScope(sortedSet, scope4);

        var expectedScope1 = "repository:my-repo:delete,pull,push";
        // Assert
        Assert.Equal(2, sortedSet.Count);
        Assert.Equal(expectedScope1, sortedSet.First().ToString());
    }

    [Fact]
    public void AddOrMergeScope_AddNewScope_AddsToSet()
    {
        // Arrange
        var scopes = new SortedSet<Scope>();
        var newScope = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });

        // Act
        Scope.AddOrMergeScope(scopes, newScope);

        // Assert
        Assert.Single(scopes);
        Assert.Equal("repository:my-repo:pull", scopes.First().ToString());
    }

    [Fact]
    public void AddOrMergeScope_MergeWithExistingScope_UnionsActions()
    {
        // Arrange
        var scopes = new SortedSet<Scope>();
        var existingScope = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });
        var newScope1 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPush, Scope.ActionDelete });
        scopes.Add(existingScope);

        // Act
        Scope.AddOrMergeScope(scopes, newScope1);

        // Assert
        Assert.Single(scopes);
        Assert.Equal("repository:my-repo:delete,pull,push", scopes.First().ToString());
    }

    [Fact]
    public void AddOrMergeScope_MergeWithWildcardAction_ResultsInWildcard()
    {
        // Arrange
        var scopes = new SortedSet<Scope>();
        var existingScope = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });
        var newScope = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionWildcard });
        scopes.Add(existingScope);

        // Act
        Scope.AddOrMergeScope(scopes, newScope);

        // Assert
        Assert.Single(scopes);
        Assert.Equal("repository:my-repo:*", scopes.First().ToString());
    }

    [Fact]
    public void AddOrMergeScope_AddDifferentScope_AddsAllToSet()
    {
        // Arrange
        var scopes = new SortedSet<Scope>();
        var scope1 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });
        var scope2 = new Scope("repository", "other-repo", new HashSet<string> { Scope.ActionPush });
        var scope3 = new Scope("registry", "catalog", new HashSet<string> { "metadata-read" });

        // Act
        Scope.AddOrMergeScope(scopes, scope1);
        Scope.AddOrMergeScope(scopes, scope2);
        Scope.AddOrMergeScope(scopes, scope3);


        // Assert
        Assert.Equal(3, scopes.Count);
        Assert.Contains(scopes, s => s.ToString() == "repository:my-repo:pull");
        Assert.Contains(scopes, s => s.ToString() == "repository:other-repo:push");
        Assert.Contains(scopes, s => s.ToString() == "registry:catalog:metadata-read");

    }

    [Fact]
    public void AddOrMergeScope_MergeWithExistingWildcardAction_KeepsWildcard()
    {
        // Arrange
        var scopes = new SortedSet<Scope>();
        var scope1 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionWildcard });
        var scope2 = new Scope("repository", "my-repo", new HashSet<string> { Scope.ActionPull });

        var scope3 = new Scope("registry", "catalog", new HashSet<string> { "metadata-read" });
        var scope4 = new Scope("registry", "catalog", new HashSet<string> { Scope.ActionWildcard });


        // Act
        Scope.AddOrMergeScope(scopes, scope1);
        Scope.AddOrMergeScope(scopes, scope2);
        Scope.AddOrMergeScope(scopes, scope3);
        Scope.AddOrMergeScope(scopes, scope4);

        // Assert
        Assert.Equal(2, scopes.Count);
        Assert.Contains(scopes, s => s.ToString() == "repository:my-repo:*");
        Assert.Contains(scopes, s => s.ToString() == "registry:catalog:*");
    }

    [Theory]
    [InlineData(Scope.Action.Pull, Scope.ActionPull)]
    [InlineData(Scope.Action.Push, Scope.ActionPush)]
    [InlineData(Scope.Action.Delete, Scope.ActionDelete)]
    [InlineData(Scope.Action.All, Scope.ActionWildcard)]
    [InlineData((Scope.Action)999, "")]
    public void ParseAction_ValidAndInvalidActions_ReturnsExpectedString(Scope.Action action, string expected)
    {
        // Act
        var result = Scope.ActionToString(action);

        // Assert
        Assert.Equal(expected, result);
    }
}
