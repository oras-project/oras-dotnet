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

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

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
        scopeManager.SetActionsForRepository(reference, null, Scope.Action.Pull);
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
        scopeManager.SetActionsForRepository(reference, null, Scope.Action.Pull, Scope.Action.Push, Scope.Action.Push);
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
        scopeManager.SetActionsForRepository(reference, null, Scope.Action.All, Scope.Action.Pull);
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
            ScopeManager.SetActionsForRepository(httpClient, reference, null, Scope.Action.Pull));

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
        ScopeManager.SetActionsForRepository(client, reference, null, Scope.Action.Pull, Scope.Action.Push);

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
        ScopeManager.SetActionsForRepository(client, reference, null, Scope.Action.Pull);
        ScopeManager.SetActionsForRepository(client, reference, null, Scope.Action.All);

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

    [Fact]
    public void GetScopesForHost_IsolatesScopes_ByPartition()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scopeA = new Scope("repository", "repo-a", new() { Scope.ActionPull });
        var scopeB = new Scope("repository", "repo-b", new() { Scope.ActionPush });

        // Act: same host, two partitions, different scopes.
        scopeManager.SetScopeForRegistry("registry1", scopeA, "partition-a");
        scopeManager.SetScopeForRegistry("registry1", scopeB, "partition-b");

        // Assert: each partition sees only its own scope.
        var scopesA = scopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(scopesA);
        Assert.Contains(scopeA, scopesA);
        Assert.DoesNotContain(scopeB, scopesA);

        var scopesB = scopeManager.GetScopesForHost("registry1", "partition-b");
        Assert.Single(scopesB);
        Assert.Contains(scopeB, scopesB);
        Assert.DoesNotContain(scopeA, scopesB);
    }

    [Fact]
    public void GetScopesForHost_DefaultPartition_IsIsolatedFromNamedPartition()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var defaultScope = new Scope("repository", "repo-default", new() { Scope.ActionPull });
        var namedScope = new Scope("repository", "repo-named", new() { Scope.ActionPull });

        // Act: default (no partition) vs a named partition on the same host.
        scopeManager.SetScopeForRegistry("registry1", defaultScope);
        scopeManager.SetScopeForRegistry("registry1", namedScope, "partition-a");

        // Assert
        var defaultScopes = scopeManager.GetScopesForHost("registry1");
        Assert.Single(defaultScopes);
        Assert.Contains(defaultScope, defaultScopes);
        Assert.DoesNotContain(namedScope, defaultScopes);

        var namedScopes = scopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(namedScopes);
        Assert.Contains(namedScope, namedScopes);
        Assert.DoesNotContain(defaultScope, namedScopes);
    }

    [Fact]
    public void GetScopesForHost_NullAndEmptyPartition_SelectSameDefaultPartition()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPull });

        // Act: store with null partition.
        scopeManager.SetScopeForRegistry("registry1", scope, null);

        // Assert: null and "" both select the default partition (consistent with Cache).
        var viaNoArg = scopeManager.GetScopesForHost("registry1");
        var viaNull = scopeManager.GetScopesForHost("registry1", null);
        var viaEmpty = scopeManager.GetScopesForHost("registry1", string.Empty);

        Assert.Single(viaNoArg);
        Assert.Single(viaNull);
        Assert.Single(viaEmpty);
        Assert.Contains(scope, viaNoArg);
        Assert.Contains(scope, viaNull);
        Assert.Contains(scope, viaEmpty);
    }

    [Fact]
    public void GetScopesStringForHost_IsolatesScopes_ByPartition()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scopeA = new Scope("repository", "repo-a", new() { Scope.ActionPull });
        var scopeB = new Scope("repository", "repo-b", new() { Scope.ActionPush });

        // Act
        scopeManager.SetScopeForRegistry("registry1", scopeA, "partition-a");
        scopeManager.SetScopeForRegistry("registry1", scopeB, "partition-b");

        // Assert
        var stringsA = scopeManager.GetScopesStringForHost("registry1", "partition-a");
        Assert.Single(stringsA);
        Assert.Equal("repository:repo-a:pull", stringsA.First());

        var stringsB = scopeManager.GetScopesStringForHost("registry1", "partition-b");
        Assert.Single(stringsB);
        Assert.Equal("repository:repo-b:push", stringsB.First());
    }

    [Fact]
    public void GetScopesStringForHost_DefaultPartition_OmittedPartitionMatchesNull()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var scope = new Scope("repository", "repo1", new() { Scope.ActionPull, Scope.ActionPush });
        scopeManager.SetScopeForRegistry("registry1", scope);

        // Act / Assert: omitting partitionId (defaulting to null) selects the default partition.
        var omitted = scopeManager.GetScopesStringForHost("registry1");
        var nullPartition = scopeManager.GetScopesStringForHost("registry1", null);

        Assert.Equal(omitted, nullPartition);
        Assert.Single(nullPartition);
        Assert.Equal("repository:repo1:pull,push", nullPartition.First());
    }

    [Fact]
    public void SetActionsForRepository_Instance_IsolatesByPartition()
    {
        // Arrange
        var scopeManager = new ScopeManager();
        var reference = new Reference("registry1", "repo1");

        // Act: same repository, different partitions, different actions.
        scopeManager.SetActionsForRepository(reference, "partition-a", Scope.Action.Pull);
        scopeManager.SetActionsForRepository(reference, "partition-b", Scope.Action.Push);

        // Assert
        var scopesA = scopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(scopesA);
        Assert.Contains(Scope.ActionPull, scopesA.First().Actions);
        Assert.DoesNotContain(Scope.ActionPush, scopesA.First().Actions);

        var scopesB = scopeManager.GetScopesForHost("registry1", "partition-b");
        Assert.Single(scopesB);
        Assert.Contains(Scope.ActionPush, scopesB.First().Actions);
        Assert.DoesNotContain(Scope.ActionPull, scopesB.First().Actions);
    }

    [Fact]
    public void SetActionsForRepository_Static_IsolatesByPartition()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var reference = new Reference("registry1", "repo1");

        // Act
        ScopeManager.SetActionsForRepository(client, reference, "partition-a", Scope.Action.Pull);
        ScopeManager.SetActionsForRepository(client, reference, "partition-b", Scope.Action.Push);

        // Assert
        var scopesA = client.ScopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(scopesA);
        Assert.Contains(Scope.ActionPull, scopesA.First().Actions);
        Assert.DoesNotContain(Scope.ActionPush, scopesA.First().Actions);

        var scopesB = client.ScopeManager.GetScopesForHost("registry1", "partition-b");
        Assert.Single(scopesB);
        Assert.Contains(Scope.ActionPush, scopesB.First().Actions);
    }

    [Fact]
    public void SetActionsForRepository_DefaultPartition_PreservesExistingBehavior()
    {
        // Arrange: a null partitionId must behave exactly as before (default partition).
        var scopeManager = new ScopeManager();
        var reference = new Reference("registry1", "repo1");

        // Act
        scopeManager.SetActionsForRepository(reference, null, Scope.Action.Pull, Scope.Action.Push);

        // Assert: present in the default partition, absent from a named partition.
        var defaultScopes = scopeManager.GetScopesForHost("registry1");
        Assert.Single(defaultScopes);
        Assert.Contains(Scope.ActionPull, defaultScopes.First().Actions);
        Assert.Contains(Scope.ActionPush, defaultScopes.First().Actions);

        Assert.Empty(scopeManager.GetScopesForHost("registry1", "partition-a"));
    }

    [Fact]
    public void SetScopeForRegistry_Static_IsolatesByPartition()
    {
        // Arrange
        var client = new Client(new HttpClient());
        var scopeA = new Scope("repository", "repo-a", new() { Scope.ActionPull });
        var scopeB = new Scope("repository", "repo-b", new() { Scope.ActionPush });

        // Act
        ScopeManager.SetScopeForRegistry(client, "registry1", scopeA, "partition-a");
        ScopeManager.SetScopeForRegistry(client, "registry1", scopeB, "partition-b");

        // Assert
        var scopesA = client.ScopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(scopesA);
        Assert.Contains(scopeA, scopesA);

        var scopesB = client.ScopeManager.GetScopesForHost("registry1", "partition-b");
        Assert.Single(scopesB);
        Assert.Contains(scopeB, scopesB);
    }

    [Fact]
    public void SetScopeForRegistry_PartitionIdContainingPipe_StaysDistinct()
    {
        // Arrange: a partitionId containing the '|' delimiter must not collide with another
        // (partitionId, registry) pair, since registry hostnames cannot contain '|'.
        var scopeManager = new ScopeManager();
        var scope1 = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var scope2 = new Scope("repository", "repo2", new() { Scope.ActionPush });

        // Act: keys are "tenant|x|registry1" and "tenant|registry1" respectively.
        scopeManager.SetScopeForRegistry("registry1", scope1, "tenant|x");
        scopeManager.SetScopeForRegistry("registry1", scope2, "tenant");

        // Assert
        var scopesPipe = scopeManager.GetScopesForHost("registry1", "tenant|x");
        Assert.Single(scopesPipe);
        Assert.Contains(scope1, scopesPipe);
        Assert.DoesNotContain(scope2, scopesPipe);

        var scopesPlain = scopeManager.GetScopesForHost("registry1", "tenant");
        Assert.Single(scopesPlain);
        Assert.Contains(scope2, scopesPlain);
        Assert.DoesNotContain(scope1, scopesPlain);
    }

    [Fact]
    public void SetScopeForRegistry_SamePartition_MergesActions()
    {
        // Arrange: merge semantics must still apply within a single partition.
        var scopeManager = new ScopeManager();
        var pull = new Scope("repository", "repo1", new() { Scope.ActionPull });
        var push = new Scope("repository", "repo1", new() { Scope.ActionPush });

        // Act
        scopeManager.SetScopeForRegistry("registry1", pull, "partition-a");
        scopeManager.SetScopeForRegistry("registry1", push, "partition-a");

        // Assert
        var scopes = scopeManager.GetScopesForHost("registry1", "partition-a");
        Assert.Single(scopes);
        Assert.Contains(Scope.ActionPull, scopes.First().Actions);
        Assert.Contains(Scope.ActionPush, scopes.First().Actions);
    }
}
