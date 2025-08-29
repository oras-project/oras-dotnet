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

using Microsoft.Extensions.Caching.Memory;
using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

public class CacheTest
{
    [Fact]
    public void SetCache_ShouldAddNewEntry_WhenRegistryDoesNotExist()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        // Act
        cache.SetCache(registry, scheme, key, token);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme));
        Assert.Equal(scheme, actualScheme);
        Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken));
        Assert.Equal(token, retrievedToken);
    }

    [Fact]
    public void SetCache_ShouldAddNewEntryForBasicToken()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Basic;
        var key = "";
        var token1 = "token1";

        // Act
        cache.SetCache(registry, scheme, key, token1);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme1));
        Assert.Equal(scheme, actualScheme1);
        Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken1));
        Assert.Equal(token1, retrievedToken1);

        // update token
        var token2 = "token2";
        cache.SetCache(registry, scheme, key, token2);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme2));
        Assert.Equal(scheme, actualScheme2);
        Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken2));
        Assert.Equal(token2, retrievedToken2);
    }

    [Fact]
    public void SetCache_ShouldUpdateSchemeAndTokens_WhenSchemeDiffers()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var initialScheme = Challenge.Scheme.Basic;
        var newScheme = Challenge.Scheme.Bearer;
        var key = "repository:repo1:delete,pull,push repository:repo2:*";
        var initialToken = "initialToken";
        var token = "testToken";

        cache.SetCache(registry, initialScheme, key, initialToken);
        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme1));
        Assert.Equal(initialScheme, actualScheme1);
        Assert.True(cache.TryGetToken(registry, initialScheme, key, out var retrievedToken1));
        Assert.Equal(initialToken, retrievedToken1);

        // Act
        cache.SetCache(registry, newScheme, key, token);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme2));
        Assert.Equal(newScheme, actualScheme2);
        Assert.True(cache.TryGetToken(registry, newScheme, key, out var retrievedToken2));
        Assert.Equal(token, retrievedToken2);
    }

    [Fact]
    public void SetCache_ShouldUpdateToken_WhenKeyExists()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "repository:repo1:delete,pull,push repository:repo2:*";
        var initialToken = "initialToken";
        var updatedToken = "updatedToken";

        cache.SetCache(registry, scheme, key, initialToken);

        // Act
        cache.SetCache(registry, scheme, key, updatedToken);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme));
        Assert.Equal(scheme, actualScheme);
        Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken));
        Assert.Equal(updatedToken, retrievedToken);
    }

    [Fact]
    public void SetCache_ShouldSetTokenWithDifferentKeys()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key1 = "repository:repo1:delete,pull,push repository:repo2:*";
        var key2 = "repository:repo3:*";

        var token1 = "token1";
        var token2 = "token2";

        cache.SetCache(registry, scheme, key1, token1);
        cache.SetCache(registry, scheme, key2, token2);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme1));
        Assert.Equal(scheme, actualScheme1);
        Assert.True(cache.TryGetToken(registry, scheme, key1, out var retrievedToken1));
        Assert.Equal(token1, retrievedToken1);
        Assert.True(cache.TryGetToken(registry, scheme, key2, out var retrievedToken2));
        Assert.Equal(token2, retrievedToken2);

        // update key2's token
        var token3 = "token3";
        cache.SetCache(registry, scheme, key2, token3);

        // Assert
        Assert.True(cache.TryGetScheme(registry, out var actualScheme2));
        Assert.Equal(scheme, actualScheme2);
        Assert.True(cache.TryGetToken(registry, scheme, key2, out var retrievedToken3));
        Assert.Equal(token3, retrievedToken3);
    }

    [Fact]
    public void TryGetToken_ShouldReturnFalse_WhenRegistryDoesNotExist()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "nonexistent.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";

        // Act
        var result = cache.TryGetToken(registry, scheme, key, out var token);

        // Assert
        Assert.False(result);
        Assert.Equal(string.Empty, token);
    }

    [Fact]
    public void TryGetToken_ShouldReturnFalse_WhenSchemeDoesNotMatch()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var storedScheme = Challenge.Scheme.Basic;
        var requestedScheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        cache.SetCache(registry, storedScheme, key, token);

        // Act
        var result = cache.TryGetToken(registry, requestedScheme, key, out var retrievedToken);

        // Assert
        Assert.False(result);
        Assert.Equal(string.Empty, retrievedToken);
    }

    [Fact]
    public void TryGetToken_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "nonexistentKey";
        var token = "testToken";

        cache.SetCache(registry, scheme, "existingKey", token);

        // Act
        var result = cache.TryGetToken(registry, scheme, key, out var retrievedToken);

        // Assert
        Assert.False(result);
        Assert.Equal(string.Empty, retrievedToken);
    }

    [Fact]
    public void TryGetToken_ShouldReturnTrueAndToken_WhenEntryExists()
    {
        // Arrange
        var cache = new Cache(new MemoryCache(new MemoryCacheOptions()));
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        cache.SetCache(registry, scheme, key, token);

        // Act
        var result = cache.TryGetToken(registry, scheme, key, out var retrievedToken);

        // Assert
        Assert.True(result);
        Assert.Equal(token, retrievedToken);
    }

    [Fact]
    public void SetCache_ShouldRespectAbsoluteExpirationOption()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new Cache(memoryCache);
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        // Set cache with a short absolute expiration, but not too short
        cache.CacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMilliseconds(300));

        // Act
        cache.SetCache(registry, scheme, key, token);

        // Verify token is initially present
        Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken));
        Assert.Equal(token, retrievedToken);

        // Wait for expiration with some buffer time
        Thread.Sleep(500);

        // Try to get the token a few times to account for potential timing issues
        bool tokenExpired = false;
        for (int i = 0; i < 3; i++)
        {
            if (!cache.TryGetToken(registry, scheme, key, out _))
            {
                tokenExpired = true;
                break;
            }
            Thread.Sleep(100);
        }

        // Assert token should expire eventually
        Assert.True(tokenExpired, "Token should expire after absolute expiration time");
    }

    [Fact]
    public void SetCache_ShouldRespectSlidingExpirationOption()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new Cache(memoryCache);
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        // Set cache with a sliding expiration - use longer duration for more reliable testing
        cache.CacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMilliseconds(500));

        // Act
        cache.SetCache(registry, scheme, key, token);

        // Verify token is initially in cache
        Assert.True(cache.TryGetToken(registry, scheme, key, out var initialToken));
        Assert.Equal(token, initialToken);

        // Access the token a few times to keep it alive
        for (int i = 0; i < 3; i++)
        {
            Thread.Sleep(200); // Wait less than the sliding expiration
            Assert.True(cache.TryGetToken(registry, scheme, key, out var retrievedToken),
                $"Token should still be in cache after {(i + 1) * 200}ms with access");
            Assert.Equal(token, retrievedToken);
        }

        // Token should still be in cache after multiple accesses
        Assert.True(cache.TryGetToken(registry, scheme, key, out var finalAccessToken));

        // Now wait longer than sliding expiration without access
        Thread.Sleep(700);

        // Assert token should expire
        bool tokenExpired = !cache.TryGetToken(registry, scheme, key, out _);
        Assert.True(tokenExpired, "Token should expire after sliding expiration time without access");
    }

    [Fact]
    public void SetCache_ShouldRespectSizeOption()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 2, // Only allow 2 cache entries
            CompactionPercentage = 0.5 // Remove 50% when limit is reached
        });
        var cache = new Cache(memoryCache)
        {
            // Set size for each cache entry
            CacheEntryOptions = new MemoryCacheEntryOptions().SetSize(1)
        };

        // Add two different registry entries
        cache.SetCache("registry1", Challenge.Scheme.Bearer, "key", "token1");
        cache.SetCache("registry2", Challenge.Scheme.Bearer, "key", "token2");

        // Verify first two are cached
        Assert.True(cache.TryGetToken("registry1", Challenge.Scheme.Bearer, "key", out _));
        Assert.True(cache.TryGetToken("registry2", Challenge.Scheme.Bearer, "key", out _));

        // Add a third entry that should trigger compaction
        cache.SetCache("registry3", Challenge.Scheme.Bearer, "key", "token3");

        // Since eviction is not deterministic, we can only verify that at least one entry is still in cache
        // This test is less strict than before but more reliable
        int entriesFound = 0;
        if (cache.TryGetToken("registry1", Challenge.Scheme.Bearer, "key", out _)) entriesFound++;
        if (cache.TryGetToken("registry2", Challenge.Scheme.Bearer, "key", out _)) entriesFound++;
        if (cache.TryGetToken("registry3", Challenge.Scheme.Bearer, "key", out _)) entriesFound++;

        // At least one entry should remain in cache (likely the most recently added one)
        Assert.True(entriesFound > 0, $"Expected at least one cache entry to remain, but found {entriesFound}");

        // The size limit and compaction should ensure we don't have all three entries
        Assert.True(entriesFound < 3, $"Expected fewer than 3 cache entries due to size limit, but found {entriesFound}");
    }

    [Fact]
    public void SetCache_ShouldAllowCustomEvictionCallback()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new Cache(memoryCache);
        var registry = "test.registry";
        var scheme = Challenge.Scheme.Bearer;
        var key = "testKey";
        var token = "testToken";

        // Use a ManualResetEvent to signal when the callback is invoked
        var callbackEvent = new ManualResetEventSlim(false);
        EvictionReason capturedReason = EvictionReason.None;

        // Set cache with post eviction callback
        cache.CacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMilliseconds(300))
            .RegisterPostEvictionCallback((_, _, reason, _) =>
            {
                capturedReason = reason;
                callbackEvent.Set();
            });

        // Act
        cache.SetCache(registry, scheme, key, token);

        // Wait for expiration
        Thread.Sleep(400);

        // Force cleanup by attempting to get the expired item
        cache.TryGetToken(registry, scheme, key, out _);

        // Wait for the callback to be invoked with timeout
        bool callbackInvoked = callbackEvent.Wait(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(callbackInvoked, "Eviction callback should have been invoked");
        Assert.Equal(EvictionReason.Expired, capturedReason);
    }
}
