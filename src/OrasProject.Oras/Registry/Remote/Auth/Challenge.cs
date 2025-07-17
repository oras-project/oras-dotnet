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

namespace OrasProject.Oras.Registry.Remote.Auth;

public static class Challenge
{
    /// <summary>
    /// Defines the supported authentication schemes.
    /// </summary>
    public enum Scheme
    {
        /// <summary>
        /// Basic authentication scheme.
        /// </summary>
        Basic,

        /// <summary>
        /// Bearer token authentication scheme.
        /// </summary>
        Bearer,

        /// <summary>
        /// Unknown or unsupported authentication scheme.
        /// </summary>
        Unknown,
    }

    private const string _specialChars = "!#$%&'*+-.^_`|~";

    /// <summary>
    /// ParseChallenge parses the "WWW-Authenticate" header returned by the remote registry
    /// and extracts parameters if scheme is Bearer.
    ///
    /// Reference:
    /// - https://datatracker.ietf.org/doc/html/rfc7235#section-2.1
    /// </summary>
    /// <param name="header">The authentication challenge header string.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Scheme"/> and a dictionary of parameters, 
    /// or <c>null</c> if no parameters are present.
    /// </returns>
    /// <exception cref="FormatException">Thrown when a quoted parameter value is not properly closed.</exception>
    public static (Scheme, Dictionary<string, string>?) ParseChallenge(string? header)
    {
        if (header == null)
        {
            return (Scheme.Unknown, null);
        }
        // as defined in RFC 7235 section 2.1, we have
        //     challenge   = auth-scheme [ 1*SP ( token68 / #auth-param ) ]
        //     auth-scheme = token
        //     auth-param  = token BWS "=" BWS ( token / quoted-string )
        //
        // since we focus parameters only on Bearer, we have
        //     challenge   = auth-scheme [ 1*SP #auth-param ]
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

        // parse params for bearer auth.
        // combining RFC 7235 section 2.1 with RFC 7230 section 7, we have
        //     #auth-param => auth-param *( OWS "," OWS auth-param )
        while (!string.IsNullOrEmpty(rest))
        {
            // split the rest string by non-token char
            var (key, remaining) = ParseToken(rest.Trim());

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(remaining) || !remaining.StartsWith("="))
            {
                break;
            }
            remaining = remaining.Substring(1).Trim();
            string value;

            // extract value if it is quoted
            if (remaining.StartsWith('"'))
            {
                var nextQuote = remaining.IndexOf('"', 1);
                if (nextQuote == -1)
                {
                    throw new FormatException("Quoted parameter value is not properly closed.");
                }

                value = remaining.Substring(1, nextQuote - 1).Trim();
                rest = remaining.Substring(nextQuote + 1).Trim();
            }
            else
            {
                (value, rest) = ParseToken(remaining.Trim());
                if (string.IsNullOrEmpty(value))
                {
                    break;
                }
            }
            paramsDictionary.Add(key, value);

            rest = rest.Trim();
            if (string.IsNullOrEmpty(rest) || !rest.StartsWith(','))
            {
                break;
            }

            rest = rest.TrimStart(',');
        }

        return (scheme, paramsDictionary);
    }

    /// <summary>
    /// ParseScheme parses the authentication scheme from a string.
    /// </summary>
    /// <param name="schemeString">The string representation of the scheme.</param>
    /// <returns>
    /// The corresponding <see cref="Scheme"/> value, or <see cref="Scheme.Unknown"/> 
    /// if the scheme is not recognized.
    /// </returns>
    internal static Scheme ParseScheme(string schemeString)
    {
        return schemeString.ToLower() switch
        {
            "basic" => Scheme.Basic,
            "bearer" => Scheme.Bearer,
            _ => Scheme.Unknown,
        };
    }

    /// <summary>
    /// ParseToken parses a token from a string, splitting it into the token and the remaining string by non-token char.
    /// </summary>
    /// <param name="token">The token string to parse.</param>
    /// <returns>
    /// A tuple containing the parsed token and the remaining string.
    /// </returns>
    internal static (string Token, string Remaining) ParseToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return (string.Empty, string.Empty);
        }

        var index = 0;
        while (index < token.Length && IsValidTokenChar(token[index]))
        {
            index++;
        }

        return (token[..index], token[index..]);
    }

    /// <summary>
    /// IsValidTokenChar determines whether a character is a valid token character defined in RFC 7230 section 3.2.6.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>
    /// <c>true</c> if the character is not a valid token character; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsValidTokenChar(char c) =>
        ('A' <= c && c <= 'Z') ||
        ('a' <= c && c <= 'z') ||
        ('0' <= c && c <= '9') ||
        _specialChars.Contains(c);
}
