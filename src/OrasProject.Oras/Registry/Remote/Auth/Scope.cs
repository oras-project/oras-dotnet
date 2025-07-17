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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OrasProject.Oras.Registry.Remote.Auth;

// Scope is defined as: <ResourceType>:<ResourceName>:<Action>,<Action>,<Action>...
// Ref: https://distribution.github.io/distribution/spec/auth/scope/
public class Scope : IComparable<Scope>
{
    /// <summary>
    /// Defines the possible actions that can be performed on a resource.
    /// </summary>
    public enum Action
    {
        /// <summary>
        /// Represents the "pull" action.
        /// </summary>
        Pull,

        /// <summary>
        /// Represents the "push" action.
        /// </summary>
        Push,

        /// <summary>
        /// Represents the "delete" action.
        /// </summary>
        Delete,

        /// <summary>
        /// Represents all actions (wildcard '*').
        /// </summary>
        All
    }

    /// <summary>
    /// ScopeRegistryCatalog is the scope for registry catalog access.
    /// </summary>
    public const string ScopeRegistryCatalog = "registry:catalog:*";

    internal const string ActionWildcard = "*";

    internal const string ActionPull = "pull";

    internal const string ActionPush = "push";

    internal const string ActionDelete = "delete";

    public required string ResourceType { get; init; }
    public required string ResourceName { get; init; }
    public required HashSet<string> Actions { get; init; }

    [SetsRequiredMembers]
    public Scope(string resourceType, string resourceName, HashSet<string> actions)
    {
        ResourceType = resourceType;
        ResourceName = resourceName;
        Actions = actions;
    }

    /// <summary>
    /// ToString converts the scope to its string representation in the format:
    /// "ResourceType:ResourceName:Action1,Action2,...".
    /// If the wildcard action '*' is present, it will be represented as "ResourceType:ResourceName:*".
    /// </summary>
    /// <returns>A string representation of the scope.</returns>
    public override string ToString()
    {
        return Actions.Contains(ActionWildcard)
            ? $"{ResourceType}:{ResourceName}:{ActionWildcard}"
            : $"{ResourceType}:{ResourceName}:{string.Join(",", Actions.OrderBy(action => action, StringComparer.OrdinalIgnoreCase))}";
    }

    /// <summary>
    /// TryParse attempts to parse a scope string into a <see cref="Scope"/> object.
    /// </summary>
    /// <param name="scopeStr">The scope string to parse.</param>
    /// <param name="scope">
    /// When this method returns, contains the parsed <see cref="Scope"/> object if the parsing succeeded;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string scopeStr, [NotNullWhen(true)] out Scope? scope)
    {
        scope = null;
        if (string.IsNullOrWhiteSpace(scopeStr))
        {
            return false;
        }

        var parts = scopeStr.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        var actions = parts[2].Split(',', StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (actions.Contains(ActionWildcard))
        {
            actions.Clear();
            actions.Add(ActionWildcard);
        }

        scope = new Scope(parts[0], parts[1], actions);
        return true;
    }

    /// <summary>
    /// ActionToString converts an <see cref="Action"/> enumeration value to its corresponding string representation.
    /// </summary>
    /// <param name="action">The <see cref="Action"/> value to be converted.</param>
    /// <returns>
    /// A string representation of the specified <paramref name="action"/>:
    /// <list type="bullet">
    /// <item><description>"pull" for <see cref="Action.Pull"/></description></item>
    /// <item><description>"push" for <see cref="Action.Push"/></description></item>
    /// <item><description>"delete" for <see cref="Action.Delete"/></description></item>
    /// <item><description>"*" for <see cref="Action.All"/></description></item>
    /// <item><description>An empty string for any other value.</description></item>
    /// </list>
    /// </returns>
    internal static string ActionToString(Action action)
    {
        return action switch
        {
            Action.Pull => ActionPull,
            Action.Push => ActionPush,
            Action.Delete => ActionDelete,
            Action.All => ActionWildcard,
            _ => ""
        };
    }

    /// <summary>
    /// AddOrMergeScope adds a new scope to the collection or merges it with an existing scope if a matching scope is found.
    /// </summary>
    /// <param name="scopes">A sorted set of existing scopes.</param>
    /// <param name="newScope">The new scope to add or merge.</param>
    /// <returns>
    /// The updated sorted set of scopes, with the new scope added or merged.
    /// </returns>
    /// <remarks>
    /// If a matching scope already exists in the set:
    /// - If either the existing scope or the new scope contains the wildcard action '*',
    ///   the existing scope's actions are cleared and replaced with the wildcard '*'.
    /// - Otherwise, the actions of the new scope are unioned with the existing scope's actions.
    /// If no matching scope exists, the new scope is added to the set.
    /// </remarks>
    public static SortedSet<Scope> AddOrMergeScope(SortedSet<Scope> scopes, Scope newScope)
    {
        if (scopes.TryGetValue(newScope, out var existingScope))
        {
            if (existingScope.Actions.Contains(ActionWildcard) || newScope.Actions.Contains(ActionWildcard))
            {
                // If either scope has the wildcard '*', clear and add '*'
                existingScope.Actions.Clear();
                existingScope.Actions.Add(ActionWildcard);
            }
            else
            {
                // Otherwise, union the actions
                existingScope.Actions.UnionWith(newScope.Actions);
            }
        }
        else
        {
            // Add the new scope if no matching scope exists
            scopes.Add(newScope);
        }
        return scopes;
    }

    /// <summary>
    /// CompareTo is to implement the method defined in the interface in IComparable.
    ///
    /// Note: This comparer is intended for use with SortedSet<Scope> in the ScopeManager.
    /// If two Scope instances have the same ResourceType and ResourceName,
    /// they are considered equivalent and will be merged into a single entry by uniting their Actions.
    /// 
    /// It compares the current <see cref="Scope"/> object with another <see cref="Scope"/> object.
    /// The comparison is based on the <see cref="ResourceType"/> and <see cref="ResourceName"/> properties.
    /// </summary>
    /// <param name="other">The other <see cref="Scope"/> object to compare to.</param>
    /// <returns>
    /// A value less than zero if this instance is less than <paramref name="other"/>;
    /// zero if this instance is equal to <paramref name="other"/>;
    /// a value greater than zero if this instance is greater than <paramref name="other"/>.
    /// </returns>
    public int CompareTo(Scope? other)
    {
        if (other == null) return 1; // Current instance is greater than null.

        // Compare ResourceType first
        int resourceTypeComparison = string.Compare(ResourceType, other.ResourceType, StringComparison.Ordinal);
        if (resourceTypeComparison != 0)
        {
            return resourceTypeComparison;
        }

        // If ResourceType is equal, compare ResourceName
        int resourceNameComparison = string.Compare(ResourceName, other.ResourceName, StringComparison.Ordinal);
        if (resourceNameComparison != 0)
        {
            return resourceNameComparison;
        }

        return 0;
    }
}
