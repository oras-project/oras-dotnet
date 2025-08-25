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

using System.Collections.Concurrent;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Registry.Remote.Auth;

public sealed class Cache(IMemoryCache memoryCache) : ICache
{
    /// <summary>
    /// CacheEntry represents a cache entry for storing authentication tokens associated with a specific challenge scheme.
    /// </summary>
    /// <param name="Scheme">The authentication scheme associated with the cache entry.</param>
    /// <param name="Tokens">A dictionary containing authentication tokens, where the key is the scopes and the value is the token itself.</param>
    private sealed record CacheEntry(Challenge.Scheme Scheme, ConcurrentDictionary<string, string> Tokens);

    /// <summary>
    /// The memory cache used to store authentication schemes and tokens.
    /// </summary>
    private readonly IMemoryCache _memoryCache = memoryCache;

    /// <summary>
    /// TryGetScheme attempts to retrieve the authentication scheme associated with the specified registry.
    /// </summary>
    /// <param name="registry">The registry for which to retrieve the authentication scheme.</param>
    /// <param name="scheme">
    /// When this method returns, contains the <see cref="Challenge.Scheme"/> associated with the specified registry
    /// if the registry exists in the cache; otherwise, <see cref="Challenge.Scheme.Unknown"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the authentication scheme for the specified registry was found in the cache; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGetScheme(string registry, out Challenge.Scheme scheme)
    {
        if (_memoryCache.TryGetValue(registry, out CacheEntry? cacheEntry) && cacheEntry != null)
        {
            scheme = cacheEntry.Scheme;
            return true;
        }

        scheme = Challenge.Scheme.Unknown;
        return false;
    }

    /// <summary>
    /// Sets or updates the cache for a specific registry and authentication scheme.
    /// </summary>
    /// <param name="registry">The registry for which the cache is being set or updated.</param>
    /// <param name="scheme">The authentication scheme associated with the cache entry.</param>
    /// <param name="key">The key used to identify the token within the cache entry.</param>
    /// <param name="token">The token to be stored in the cache.</param>
    /// <remarks>
    /// If the registry already exists in the cache:
    /// - If the provided scheme differs from the existing scheme, the cache entry is replaced with a new one.
    /// - Otherwise, the token is added or updated in the existing cache entry.
    /// </remarks>
    public void SetCache(string registry, Challenge.Scheme scheme, string key, string token)
    {
        if (_memoryCache.TryGetValue(registry, out CacheEntry? oldEntry) &&
            oldEntry != null &&
            scheme == oldEntry.Scheme)
        {
            // When the scheme matches, update the token in the existing entry atomically
            oldEntry.Tokens.AddOrUpdate(key, token, (_, _) => token);
            return;
        }

        // Otherwise, set a new entry
        var tokens = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
        {
            [key] = token
        };
        var newEntry = new CacheEntry(scheme, tokens);
        _memoryCache.Set(registry, newEntry);
    }

    /// <summary>
    /// TryGetToken attempts to retrieve a token from the cache for the specified registry, authentication scheme, and key.
    /// </summary>
    /// <param name="registry">The registry for which the token is being requested.</param>
    /// <param name="scheme">The authentication scheme associated with the token.</param>
    /// <param name="key">The key used to identify the token within the cache.</param>
    /// <param name="token">
    /// When this method returns, contains the token associated with the specified registry, scheme, and key,
    /// if the token is found; otherwise, an empty string.
    /// </param>
    /// <returns>
    /// <c>true</c> if a token matching the specified registry, scheme, and key is found in the cache; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGetToken(string registry, Challenge.Scheme scheme, string key, out string token)
    {
        if (_memoryCache.TryGetValue(registry, out CacheEntry? cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.Scheme == scheme)
        {
            if (cacheEntry.Tokens.TryGetValue(key, out string? cachedToken) &&
                !string.IsNullOrEmpty(cachedToken))
            {
                token = cachedToken;
                return true;
            }
        }

        token = string.Empty;
        return false;
    }
}
