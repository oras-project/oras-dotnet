using System;
using System.Collections.Generic;

namespace OrasProject.Oras.Registry.Remote.Auth;

public class Challenge
{
    public enum Scheme
    {
        Basic,
        Bearer,
        Unknown,
    }

    public static (Scheme, Dictionary<string, string>?) ParseChallenge(string header)
    {
        var (schemeString, rest) = ParseToken(header);
        var scheme = ParseScheme(schemeString);

        if (scheme != Scheme.Bearer)
        {
            return (scheme, null);
        }
        
        rest = rest.Trim();
        if (string.IsNullOrEmpty(rest))
        {
            return (scheme, null);
        }
        
        var paramsDictionary = new Dictionary<string, string>();
        var parameters = rest.Split(",");

        foreach (var parameter in parameters)
        {
            var (key, remaining) = ParseToken(parameter.Trim());
            remaining = remaining.Trim();
            if (string.IsNullOrEmpty(remaining) || !remaining.StartsWith("="))
            {
                continue;
            }
            
            var value = remaining.Substring(1);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value.Substring(1, value.Length - 2).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                paramsDictionary.Add(key, value);
            }
            else
            {
                paramsDictionary.Add(key, value);
            }
        }
        
        return (scheme, paramsDictionary);
    }
    
    private static Scheme ParseScheme(string schemeString)
    {
        return schemeString.ToLower() switch
        {
            "basic" => Scheme.Basic,
            "bearer" => Scheme.Bearer,
            _ => Scheme.Unknown,
        };
    }
    
    private static (string, string) ParseToken(string token)
    {
        var index = Array.FindIndex(token.ToCharArray(), c => IsNotTokenChar(c));
        if (index == -1)
        {
            return (token.Substring(0, index), token.Substring(index));
        }

        return (token, "");
    }
    
    private static bool IsNotTokenChar(char c)
    {
        // Check if character is not in the valid ranges (A-Z, a-z, 0-9)
        if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9'))
        {
            // Check if the character is not one of the special characters in the list
            string specialChars = "!#$%&'*+-.^_`|~";
            return !specialChars.Contains(c);
        }
        return false;
    }
}
