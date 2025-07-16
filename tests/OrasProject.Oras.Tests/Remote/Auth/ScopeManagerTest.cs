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
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Remote.Auth;

public class ScopeManagerTest
{
    [Fact]
    public void SetScopeForRegistry_AddsNewScope_WhenRegistryDoesNotExist()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scope = new Scope
        ("repository",
            "repo1",
            new() { Scope.ActionPull }
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
        var scopeManager = new ScopeManager();
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPull }
        );
        var scope2 = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPush }
        );

        var scope3 = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPush }
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
        Assert.Contains(Scope.ActionPull, mergedScope.Actions);
        Assert.Contains(Scope.ActionPush, mergedScope.Actions);
    }

    [Fact]
    public void SetScopeForRegistry_WithDifferentRepos()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPull }
        );
        var scope2 = new Scope
        (
            "registry",
            "catalog",
            new() { Scope.ActionPush, Scope.ActionWildcard }
        );

        var scope3 = new Scope
        (
            "registry",
            "catalog",
            new() { Scope.ActionPull }
        );

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        scopeManager.SetScopeForRegistry("registry1", scope3);

        var results = scopeManager.GetScopesForHost("registry1");

        string[] expectedResources = ["registry", "catalog", "repository", "repo1"];
        string[] expectedActions = [Scope.ActionWildcard, Scope.ActionPull];

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
        var scopeManager = new ScopeManager();
        var scope1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var scope2 = new Scope("repository", "repo1", new() { Scope.ActionPush });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var mergedScope = result.First();
        Assert.Contains(Scope.ActionPull, mergedScope.Actions);
        Assert.Contains(Scope.ActionPush, mergedScope.Actions);

        var newScopes = new SortedSet<Scope>
        {
            scope1,
            scope2
        };

        result.UnionWith(newScopes);
        Assert.Single(result);
        mergedScope = result.First();
        Assert.Contains(Scope.ActionPull, mergedScope.Actions);
        Assert.Contains(Scope.ActionPush, mergedScope.Actions);

    }

    [Fact]
    public void SetScopeForRegistry_ReplacesActionsWithAll_WhenAllActionIsAdded()
    {

        // Arrange
        var scopeManager = new ScopeManager();
        var scope1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var scope2 = new Scope("repository", "repo1", new() { Scope.ActionWildcard });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scope1);
        scopeManager.SetScopeForRegistry("registry1", scope2);
        var result = scopeManager.GetScopesForHost("registry1");

        // Assert
        Assert.Single(result);
        var updatedScope = result.First();
        Assert.Single(updatedScope.Actions);
        Assert.Contains(Scope.ActionWildcard, updatedScope.Actions);
    }

    [Fact]
    public void SetScopeForRegistry_DoesNotDuplicateScopes_WhenSameScopeIsAdded()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPull });

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
        var scopeManager = new ScopeManager();
        var scope1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var scope2 = new Scope("repository", "repo2", new() { Scope.ActionPush });

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
        var scopeManager = new ScopeManager();
        var scope = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPush, Scope.ActionPull }
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
        var scopeManager = new ScopeManager();

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
        var scopeManager = new ScopeManager();
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPush, Scope.ActionPull, Scope.ActionDelete });
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
        var scopeManager = new ScopeManager();
        var scope1 = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionPull }
        );

        var scope2 = new Scope
        (
            "repository",
            "repo2",
            new() { Scope.ActionPush, Scope.ActionPull }
        );

        var scope3 = new Scope
        (
            "repository",
            "repo3",
            new() { Scope.ActionPush, Scope.ActionPull, Scope.ActionDelete, Scope.ActionWildcard }
        );

        var scope4 = new Scope
        (
            "registry",
            "catalog",
            new() { Scope.ActionPush, Scope.ActionPull, Scope.ActionDelete, Scope.ActionWildcard }
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
        var scopeManager = new ScopeManager();
        var scope = new Scope
        (
            "repository",
            "repo1",
            new() { Scope.ActionWildcard, Scope.ActionPull, Scope.ActionDelete }
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
        var scopeManager = new ScopeManager();

        // Act
        var result = scopeManager.GetScopesForHost("nonexistent-registry");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void SetRepositoryScope_AddsScopeForValidReference()
    {


        // Arrange
        var scopeManager = new ScopeManager();
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
        Assert.Contains(Scope.ActionPull, scope.Actions);

    }

    [Fact]
    public void SetRepositoryScope_MergesActions_WhenScopeAlreadyExists()
    {


        // Arrange
        var scopeManager = new ScopeManager();
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
        Assert.Contains(Scope.ActionPull, scope.Actions);
        Assert.Contains(Scope.ActionPush, scope.Actions);

    }

    [Fact]
    public void SetRepositoryScope_ReplacesActionsWithAll_WhenAllActionIsAdded()
    {


        // Arrange
        var scopeManager = new ScopeManager();
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
        Assert.Contains(Scope.ActionWildcard, scope.Actions);

    }

    [Fact]
    public void SetActionsForRepository_DoesNothing_ForPlainHttpClient()
    {
        // Arrange
        var httpClient = new PlainClient(new HttpClient());
        var reference = new Reference("registry1", "repo1");

        // Act
        var exc = Record.Exception(() =>
            ScopeManager.SetActionsForRepository(httpClient, reference, Scope.Action.Pull));

        // Assert
        Assert.Null(exc);
    }

    [Fact]
    public void SetActionsForRepository_AddsScope_WhenCalledOnClient()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var reference = new Reference("registry1", "repo1");

        // Act
        ScopeManager.SetActionsForRepository(client, reference, Scope.Action.Pull, Scope.Action.Push);

        // Assert
        var scopes = client.ScopeManager.GetScopesForHost("registry1");
        Assert.Single(scopes);
        var scope = scopes.First();
        Assert.Equal("repository", scope.ResourceType);
        Assert.Equal("repo1", scope.ResourceName);
        Assert.Contains(Scope.ActionPull, scope.Actions);
        Assert.Contains(Scope.ActionPush, scope.Actions);
    }

    [Fact]
    public void SetActionsForRepository_ReplacesWithAll_WhenAllActionIncluded()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var reference = new Reference("registry1", "repo1");

        // Act
        ScopeManager.SetActionsForRepository(client, reference, Scope.Action.Pull);
        ScopeManager.SetActionsForRepository(client, reference, Scope.Action.All);

        // Assert
        var scopes = client.ScopeManager.GetScopesForHost("registry1");
        Assert.Single(scopes);
        var scope = scopes.First();
        Assert.Single(scope.Actions);
        Assert.Contains(Scope.ActionWildcard, scope.Actions);
    }


    [Fact]
    public void SetScopeForRegistry_Static_DoesNothing_ForPlainHttpClient()
    {
        // Arrange
        var httpClient = new PlainClient(new HttpClient());
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPull });

        // Act / Assert
        var ex = Record.Exception(() =>
            ScopeManager.SetScopeForRegistry(httpClient, "registry1", scope));

        // no exception, no scopes stored anywhere
        Assert.Null(ex);
    }

    [Fact]
    public void SetScopeForRegistry_Static_AddsScope_WhenCalledOnClient()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPull, Scope.ActionPush });

        // Act
        ScopeManager.SetScopeForRegistry(client, "registry1", scope);

        // Assert
        var scopes = client.ScopeManager.GetScopesForHost("registry1");
        Assert.Single(scopes);
        var result = scopes.First();
        Assert.Equal("repository", result.ResourceType);
        Assert.Equal("repo1", result.ResourceName);
        Assert.Contains(Scope.ActionPull, result.Actions);
        Assert.Contains(Scope.ActionPush, result.Actions);
    }

    [Fact]
    public void SetScopeForRegistry_Static_MergesActions_WhenCalledMultipleTimes()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var s1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var s2 = new Scope("repository", "repo1", new() { Scope.ActionPush });

        // Act
        ScopeManager.SetScopeForRegistry(client, "registry1", s1);
        ScopeManager.SetScopeForRegistry(client, "registry1", s2);

        // Assert
        var scopes = client.ScopeManager.GetScopesForHost("registry1");
        Assert.Single(scopes);
        var merged = scopes.First();
        Assert.Contains(Scope.ActionPull, merged.Actions);
        Assert.Contains(Scope.ActionPush, merged.Actions);
    }

    [Fact]
    public void SetScopeForRegistry_Static_ReplacesWithAll_WhenAllActionIncluded()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var s1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var s2 = new Scope("repository", "repo1", new() { Scope.ActionWildcard });

        // Act
        ScopeManager.SetScopeForRegistry(client, "registry1", s1);
        ScopeManager.SetScopeForRegistry(client, "registry1", s2);

        // Assert
        var scopes = client.ScopeManager.GetScopesForHost("registry1");
        Assert.Single(scopes);
        var updated = scopes.First();
        Assert.Single(updated.Actions);
        Assert.Contains(Scope.ActionWildcard, updated.Actions);
    }

}
