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
    public static readonly string ScopeRegistryCatalog = "registry:catalog:*";
    
    public required string ResourceType { get; init; }
    public required string ResourceName { get; init; }
    public required HashSet<Action> Actions { get; init; }

    [SetsRequiredMembers]
    public Scope(string resourceType, string resourceName, HashSet<Action> actions)
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
        return Actions.Contains(Action.All) 
            ? $"{ResourceType}:{ResourceName}:*"
            : $"{ResourceType}" + 
              $":{ResourceName}" + 
              $":{string.Join(",", Actions.OrderBy(action => action.ToString()).Select(action => action.ToString().ToLower()))}";
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
        if (string.IsNullOrEmpty(scopeStr))
        {
            return false;
        }

        var parts = scopeStr.Trim().Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        var actionsStr = parts[2].Split(',');
        var actions = new HashSet<Action>();
        foreach (var actionStr in actionsStr)
        {
            if (TryParseAction(actionStr.Trim(), out var action))
            {
                if (action == Action.All)
                {
                    actions.Clear();
                    actions.Add(action.Value);
                    break;
                }
                actions.Add(action.Value);
            }
            else
            {
                return false;
            }
        }

        scope = new Scope(parts[0].Trim(), parts[1].Trim(), actions);
        return true;
    }

    /// <summary>
    /// TryParseAction attempts to parse an action string into a <see cref="Action"/> enum value.
    /// </summary>
    /// <param name="actionStr">The action string to parse.</param>
    /// <param name="action">
    /// When this method returns, contains the parsed <see cref="Action"/> value if the parsing succeeded;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the parsing succeeded; otherwise, <c>false</c>.</returns>
    internal static bool TryParseAction(string actionStr, [NotNullWhen(true)]out Action? action)
    {
        switch (actionStr.ToLower())
        {
            case "pull":
                action = Action.Pull;
                return true;
            case "push":
                action = Action.Push;
                return true;
            case "delete":
                action = Action.Delete;
                return true;
            case "*":
                action = Action.All;
                return true;
            default:
                action = null;
                return false;
        }
    }

    /// <summary>
    /// CompareTo is to implement the method defined in the interface in IComparable.
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
