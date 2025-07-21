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

using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Remote.Auth;

public class CacheTest
{
    [Fact]
    public void SetCache_ShouldAddNewEntry_WhenRegistryDoesNotExist()
    {
        // Arrange
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
        var cache = new Cache();
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
}
