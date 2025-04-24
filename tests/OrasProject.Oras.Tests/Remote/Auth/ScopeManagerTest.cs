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

using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Remote.Auth;

public class ScopeManagerTest : IDisposable
{
    public void Dispose()
    {
        ScopeManager.ResetInstance();
    }
    
    [Fact]
    public void SetScopeForRegistry_AddsNewScope_WhenRegistryDoesNotExist()
    {
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope
        ("repository",
            "repo1",
            new () { "pull" }
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Contains(scope, result);
    }

    [Fact]
    public void SetScopeForRegistry_MergesActions_WhenScopeAlreadyExists()
    {
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { "pull" }
        );
        var scope2 = new Scope
        (
            "repository",
            "repo1",
            new () { "push" }
        );
        
        var scope3 = new Scope
        (
            "repository",
            "repo1",
            new () { "push" }
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
        Assert.Contains("pull", mergedScope.Actions);
        Assert.Contains("push", mergedScope.Actions);
    }
    
    [Fact]
    public void SetScopeForRegistry_WithDifferentRepos()
    {
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { "pull" }
        );
        var scope2 = new Scope
        (
            "registry",
            "catalog",
            new () { "push", "*" }
        );
        
        var scope3 = new Scope
        (
            "registry",
            "catalog",
            new () { "pull"}
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        scopeManager.SetScopeForRegistry("registry1", scope3);

        var results = scopeManager.GetScopesForHost("registry1");
        
        string[] expectedResources = ["registry", "catalog", "repository", "repo1"];
        string[] expectedActions = ["*", "pull"];
        
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
        
    }

    
    [Fact]
    public void SetScopeForRegistry_MergesActions_WhenScopeExists()
    {
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { "pull" });
        var scope2 = new Scope("repository", "repo1", new() { "push" });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var mergedScope = result.First();
        Assert.Contains("pull", mergedScope.Actions);
        Assert.Contains("push", mergedScope.Actions);

        var newScopes = new SortedSet<Scope>
        {
            scope1,
            scope2
        };
        
        result.UnionWith(newScopes);
        Assert.Single(result);
        mergedScope = result.First();
        Assert.Contains("pull", mergedScope.Actions);
        Assert.Contains("push", mergedScope.Actions);
        
    }

    [Fact]
    public void SetScopeForRegistry_ReplacesActionsWithAll_WhenAllActionIsAdded()
    {
        
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { "pull" });
        var scope2 = new Scope("repository", "repo1", new() { "*" });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var updatedScope = result.First();
        Assert.Single(updatedScope.Actions);
        Assert.Contains("*", updatedScope.Actions);
    }

    [Fact]
    public void SetScopeForRegistry_DoesNotDuplicateScopes_WhenSameScopeIsAdded()
    {
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope("repository", "repo1", new() { "pull" });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope);
        scopeManager.SetScopeForRegistry("registry1", scope);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Contains(scope, result);
        
    }

    [Fact]
    public void SetScopeForRegistry_AddsMultipleScopes_ForDifferentRepositories()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope("repository", "repo1", new() { "pull" });
        var scope2 = new Scope("repository", "repo2", new() { "push" });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(scope1, result);
        Assert.Contains(scope2, result);
        
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsCorrectStrings()
    {
        
        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope =  new Scope
        (
            "repository",
            "repo1",
            new () { "push", "pull" }
        );
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:pull,push", result.First());
        
    }


    [Fact]
    public void GetScopesStringForHost_ReturnsEmptyList_WhenNoScopesExistForRegistry()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;

        // Act
        var result = scopeManager.GetScopesStringForHost("empty-registry");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsSortedActions_ForSingleScope()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope("repository", "repo1", new() { "push", "pull", "delete" });
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:delete,pull,push", result.First());
        
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsFormattedStrings_ForMultipleScopes()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new () { "pull" }
        );
        
        var scope2 = new Scope
        (
            "repository",
            "repo2",
            new () { "push", "pull" }
        );
        
        var scope3 = new Scope
        (
            "repository",
            "repo3",
            new () { "push", "pull", "delete", "*" }
        );
        
        var scope4 = new Scope
        (
            "registry",
            "catalog",
            new () { "push", "pull", "delete", "*" }
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
        
    }
    
    [Fact]
    public void GetScopesStringForHost_ReturnsFormattedStrings_ForWildCardScope()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var scope = new Scope
        (
            "repository",
            "repo1",
            new () { "*", "pull", "delete" }
        );
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act
        var result = scopeManager.GetScopesStringForHost("registry1");

        // Assert
        Assert.Single(result);
        Assert.Equal("repository:repo1:*", result.First());
        
    }
    
    [Fact]
    public void GetScopesForHost_ReturnsEmptySet_WhenRegistryNotFound()
    {
        

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
        

        var instance1 = ScopeManager.Instance;
        var instance2 = ScopeManager.Instance;
        
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void SetRepositoryScope_AddsScopeForValidReference()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var reference = new Reference
        (
            "registry1",
            "repo1"
        );
        
        // Act
        scopeManager.SetActionsForRepository(reference, Scope.Action.Pull);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var scope = result.First();
        Assert.Equal("repository", scope.ResourceType);
        Assert.Equal("repo1", scope.ResourceName);
        Assert.Contains("pull", scope.Actions);
        
    }

    [Fact]
    public void SetRepositoryScope_MergesActions_WhenScopeAlreadyExists()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var reference = new Reference
        (
            "registry1",
            "repo1"
        );

        // Act
        scopeManager.SetActionsForRepository(reference, Scope.Action.Pull, Scope.Action.Push, Scope.Action.Push);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var scope = result.First();
        Assert.Equal("repository", scope.ResourceType);
        Assert.Equal("repo1", scope.ResourceName);
        Assert.Equal(2, scope.Actions.Count);
        Assert.Contains("pull", scope.Actions);
        Assert.Contains("push", scope.Actions);
        
    }

    [Fact]
    public void SetRepositoryScope_ReplacesActionsWithAll_WhenAllActionIsAdded()
    {
        

        // Arrange
        var scopeManager = ScopeManager.Instance;
        var reference = new Reference
        (
            "registry1",
            "repo1"
        );

        // Act
        scopeManager.SetActionsForRepository(reference, Scope.Action.All, Scope.Action.Pull);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var scope = result.First();
        Assert.Single(scope.Actions);
        Assert.Contains("*", scope.Actions);
        
    }
}
