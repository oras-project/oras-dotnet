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

public class ScopeManagerTest
{
    [Fact]
    public void SetScopeForRegistry_AddsNewScope_WhenRegistryDoesNotExist()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope
        ("repository",
            "repo1",
            new () { Scope.Action.Pull }
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Contains(scope, result);
        ScopeManager.ResetInstanceForTesting();
    }

    [Fact]
    public void SetScopeForRegistry_MergesActions_WhenScopeAlreadyExists()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Pull }
        );
        var scope2 = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Push }
        );
        
        var scope3 = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Push }
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        scopeManager.SetScopeForRegistry("registry1", scope3);

        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var mergedScope = result.First();
        Assert.Equal("repository", mergedScope.ResourceType);
        Assert.Equal("repo1", mergedScope.ResourceName);
        Assert.Contains(Scope.Action.Pull, mergedScope.Actions);
        Assert.Contains(Scope.Action.Push, mergedScope.Actions);
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void SetScopeForRegistry_WithDifferentRepos()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Pull }
        );
        var scope2 = new Scope
        (
            "registry",
            "catalog",
            new () { Scope.Action.Push, Scope.Action.All }
        );
        
        var scope3 = new Scope
        (
            "registry",
            "catalog",
            new () { Scope.Action.Pull }
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        scopeManager.SetScopeForRegistry("registry1", scope3);

        var results = scopeManager.GetScopesForHost("registry1");
        
        string[] expectedResources = ["registry", "catalog", "repository", "repo1"];
        Scope.Action[] expectedActions = [Scope.Action.All, Scope.Action.Pull];
        // Assert
        Assert.Equal(2, results.Count);
        var resourceIndex = 0;
        var actionIndex = 0;
        foreach (var result in results)
        {
            Assert.Contains(expectedActions[actionIndex++], result.Actions);
            Assert.Equal(expectedResources[resourceIndex++], result.ResourceType);
            Assert.Equal(expectedResources[resourceIndex++], result.ResourceName);
        }
        ScopeManager.ResetInstanceForTesting();
    }

    
    [Fact]
    public void SetScopeForRegistry_MergesActions_WhenScopeExists()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { Scope.Action.Pull });
        var scope2 = new Scope("repository", "repo1", new() { Scope.Action.Push });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var mergedScope = result.First();
        Assert.Contains(Scope.Action.Pull, mergedScope.Actions);
        Assert.Contains(Scope.Action.Push, mergedScope.Actions);

        var newScopes = new SortedSet<Scope>
        {
            scope1,
            scope2
        };
        
        result.UnionWith(newScopes);
        Assert.Single(result);
        mergedScope = result.First();
        Assert.Contains(Scope.Action.Pull, mergedScope.Actions);
        Assert.Contains(Scope.Action.Push, mergedScope.Actions);
        ScopeManager.ResetInstanceForTesting();
    }

    [Fact]
    public void SetScopeForRegistry_ReplacesActionsWithAll_WhenAllActionIsAdded()
    {
        ScopeManager.ResetInstanceForTesting();
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { Scope.Action.Pull });
        var scope2 = new Scope("repository", "repo1", new() { Scope.Action.All });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var updatedScope = result.First();
        Assert.Single(updatedScope.Actions);
        Assert.Contains(Scope.Action.All, updatedScope.Actions);
        ScopeManager.ResetInstanceForTesting();

    }

    [Fact]
    public void SetScopeForRegistry_DoesNotDuplicateScopes_WhenSameScopeIsAdded()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope("repository", "repo1", new() { Scope.Action.Pull });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope);
        scopeManager.SetScopeForRegistry("registry1", scope);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Contains(scope, result);
        ScopeManager.ResetInstanceForTesting();
    }

    [Fact]
    public void SetScopeForRegistry_AddsMultipleScopes_ForDifferentRepositories()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { Scope.Action.Pull });
        var scope2 = new Scope("repository", "repo2", new() { Scope.Action.Push });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(scope1, result);
        Assert.Contains(scope2, result);
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsCorrectStrings()
    {
        ScopeManager.ResetInstanceForTesting();
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope =  new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Push, Scope.Action.Pull }
        );
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:pull,push", result.First());
        ScopeManager.ResetInstanceForTesting();
    }


    [Fact]
    public void GetScopesStringForHost_ReturnsEmptyList_WhenNoScopesExistForRegistry()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;

        // Act
        var result = scopeManager.GetScopesStringForHost("empty-registry");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsSortedActions_ForSingleScope()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope("repository", "repo1", new() { Scope.Action.Push, Scope.Action.Pull, Scope.Action.Delete });
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:delete,pull,push", result.First());
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsFormattedStrings_ForMultipleScopes()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.Pull }
        );
        
        var scope2 = new Scope
        (
            "repository",
            "repo2",
            new () { Scope.Action.Push, Scope.Action.Pull }
        );
        
        var scope3 = new Scope
        (
            "repository",
            "repo3",
            new () { Scope.Action.Push, Scope.Action.Pull, Scope.Action.Delete, Scope.Action.All }
        );
        
        var scope4 = new Scope
        (
            "registry",
            "catalog",
            new () { Scope.Action.Push, Scope.Action.Pull, Scope.Action.Delete, Scope.Action.All }
        );
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        scopeManager.SetScopeForRegistry("registry1", scope3);
        scopeManager.SetScopeForRegistry("registry1", scope4);


        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains("repository:repo1:pull", result);
        Assert.Contains("repository:repo2:pull,push", result);
        Assert.Contains("repository:repo3:*", result);
        Assert.Contains("registry:catalog:*", result);
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsFormattedStrings_ForWildCardScope()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope
        (
            "repository",
            "repo1",
            new () { Scope.Action.All, Scope.Action.Pull, Scope.Action.Delete }
        );
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:*", result.First());
        ScopeManager.ResetInstanceForTesting();
    }
    
    [Fact]
    public void GetScopesForHost_ReturnsEmptySet_WhenRegistryNotFound()
    {
        ScopeManager.ResetInstanceForTesting();

        // Arrange
        var scopeManager = ScopeManager.Instance;

        // Act
        var result = scopeManager.GetScopesForHost("nonexistent-registry");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void TestSameScopeManagerInstance()
    {
        ScopeManager.ResetInstanceForTesting();

        var instance1 = ScopeManager.Instance;
        var instance2 = ScopeManager.Instance;
        
        Assert.Same(instance1, instance2);
    }
}
