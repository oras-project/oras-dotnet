using System.Collections;
using System.Collections.Generic;

namespace OrasProject.Oras.Registry.Remote.Auth;

internal class Cache
{
    internal Dictionary<string, Challenge.Scheme> Schemes = new ();
    internal Dictionary<string, Dictionary<string, string>> Tokens = new ();
    internal Dictionary<string, string> Scopes = new();

    internal Challenge.Scheme? GetScheme(string? host)
    {
        return Schemes.GetValueOrDefault(host);
    }

    internal string? GetToken(string registry, Challenge.Scheme scheme)
    {
        return Tokens.GetValueOrDefault(registry)?.GetValueOrDefault(scheme.ToString());
    }
    
    internal string? GetToken(string registry, Challenge.Scheme scheme, IList<string> scopes)
    {
        return Tokens.GetValueOrDefault(registry)?.GetValueOrDefault(scheme.ToString());
    }
    
    internal void SetToken(string registry, string token, Challenge.Scheme scheme) {
        
    }

    
    
}
