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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace OrasProject.Oras.Registry.Remote.Auth;

public class ScopeManager
{
    /// <summary>
    /// A thread-safe dictionary that maps a string key to a sorted set of <see cref="Scope"/> objects.
    /// This is used to manage and organize scopes in a concurrent environment.
    /// </summary>
    private ConcurrentDictionary<string, SortedSet<Scope>> Scopes { get; } = new ();
    
    /// <summary>
    /// GetScopesForHost returns a sorted set of scopes for the given registry if found,
    /// otherwise, returns empty sorted set.
    /// </summary>
    /// <param name="registry"></param>
    /// <returns></returns>
    public SortedSet<Scope> GetScopesForHost(string registry)
    {
        return Scopes.TryGetValue(registry, out var scopes) ? scopes : new();
    }
    
    /// <summary>
    /// GetScopesStringForHost returns a list of scopes string for the given registry if found,
    /// otherwise, returns empty list.
    /// </summary>
    /// <param name="registry"></param>
    /// <returns></returns>
    public List<string> GetScopesStringForHost(string registry)
    {
        return Scopes.TryGetValue(registry, out var scopes)
            ? scopes.Select(scope => scope.ToString()).ToList() 
            : new();
    }

    /// <summary>
    /// SetActionsForRepository sets actions for the given repository if the httpClient is auth.Client.
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="reference"></param>
    /// <param name="actions"></param>
    public static void SetActionsForRepository(HttpClient httpClient, Reference reference, params Scope.Action[] actions)
    {
        if (httpClient is Client authClient)
        {
            authClient.ScopeManager.SetActionsForRepository(reference, actions);
        }
    }

    /// <summary>
    /// SetActionsForRepository sets the actions for a repository by creating a scope and associating it with the specified registry.
    /// </summary>
    /// <param name="reference">
    /// The reference object containing the registry and repository information.
    /// </param>
    /// <param name="actions">
    /// The actions to be associated with the repository scope.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the registry or repository in the <paramref name="reference"/> is null.
    /// </exception>
    public void SetActionsForRepository(Reference reference, params Scope.Action[] actions)
    {
        var registry = reference.Registry ?? throw new ArgumentNullException(reference.Registry);
        var repository = reference.Repository ?? throw new ArgumentNullException(reference.Repository);

        var scope = new Scope(
            resourceType: "repository",
            resourceName: repository,
            actions: actions.Select(Scope.ActionToString).ToHashSet(StringComparer.OrdinalIgnoreCase));

        SetScopeForRegistry(registry, scope);
    }
    
    /// <summary>
    /// SetScopeForRegistry sets scope for the given registry when the httpclient is auth.Client.
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="registry"></param>
    /// <param name="scope"></param>
    public static void SetScopeForRegistry(HttpClient httpClient, string registry, Scope scope)
    {
        if (httpClient is Client authClient)
        {
            authClient.ScopeManager.SetScopeForRegistry(registry, scope);
        }
    }

    /// <summary>
    /// SetScopeForRegistry sets the scope for a specific registry. If the scope contains the "All" action, 
    /// it ensures that only the "All" action is retained. Otherwise, it merges the actions 
    /// of the provided scope with any existing scope for the registry.
    /// </summary>
    /// <param name="registry">The registry for which the scope is being set.</param>
    /// <param name="scope">The scope to be set for the registry, including its actions.</param>
    public void SetScopeForRegistry(string registry, Scope scope)
    {
        if (scope.Actions.Contains(Scope.Wildcard))
        {
            scope.Actions.Clear();
            scope.Actions.Add(Scope.Wildcard);
        }

        Scopes.AddOrUpdate(registry,
            new SortedSet<Scope> { scope },
            (_, existingScopes) => Scope.AddOrMergeScope(existingScopes, scope));
    }
}
