using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace OrasProject.Oras.Registry.Remote.Auth;

public class ScopeManager
{
    public enum Action
    {
        Pull,
        Push,
        Delete,
    }

    private static Lazy<ScopeManager> _instance => new (() => new ScopeManager());
    
    public static ScopeManager Instance => _instance.Value;
    
    public Dictionary<string, Dictionary<string, HashSet<Action>>> Scopes { get; private set; } = new ();

    public IList<string> GetAllScopesForHost(string registry)
    {
        if (!Scopes.ContainsKey(registry))
        {
            return ImmutableArray<string>.Empty;
        }
        
        var scopes = Scopes.GetValueOrDefault(registry);
        var result = new List<string>();
        foreach (var (repository, actions) in scopes)
        {
            result.Add($"repository:{repository}:{string.Join(",", actions)}");
        }

        return result;
    }
}
