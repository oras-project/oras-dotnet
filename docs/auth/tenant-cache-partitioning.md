# Auth Cache Enhancement Plan: Simple Cache Partitioning with `tenantId`

## Executive Summary

This document proposes a simple enhancement to the ORAS .NET authentication cache to support flexible cache key partitioning. The current implementation partitions tokens by registry host only, which is insufficient for multi-tenant scenarios where different credentials may be required for the same upstream registry based on downstream context (e.g., customer ID, destination reference, or sync job).

**Key Design Principle:** The SDK accepts a simple `tenantId` string from the consumer. The consumer is responsible for deciding what this ID should be (hash of reference, customer ID, etc.). The SDK simply uses it as a prefix to partition the cache.

---

## 1. Current Implementation

### 1.1 Overview

The auth cache (`Cache.cs`) stores OAuth2/Basic auth tokens for reuse across requests to the same registry. This avoids repeated token fetches and improves performance.

### 1.2 Key Components

| Component | File | Purpose |
|-----------|------|---------|
| `ICache` | `Registry/Remote/Auth/ICache.cs` | Cache interface |
| `Cache` | `Registry/Remote/Auth/Cache.cs` | Default implementation using `IMemoryCache` |
| `Client` | `Registry/Remote/Auth/Client.cs` | HTTP client that uses cache for auth |
| `Scope` | `Registry/Remote/Auth/Scope.cs` | OAuth2 permission scope (e.g., `repository:nginx:pull`) |
| `ScopeManager` | `Registry/Remote/Auth/ScopeManager.cs` | Tracks scopes per registry |
| `ICredentialProvider` | `Registry/Remote/Auth/ICredentialProvider.cs` | Resolves credentials for a registry |

### 1.3 Current Cache Structure

```
┌─────────────────────────────────────────────────────────────────────────┐
│ IMemoryCache                                                            │
├─────────────────────────────────────────────────────────────────────────┤
│ Key: "ORAS_AUTH_docker.io"                                              │
│ Value: CacheEntry                                                       │
│   ├── Scheme: Bearer                                                    │
│   └── Tokens: Dictionary<string, string>                                │
│         ├── "repository:library/nginx:pull" → "eyJhbGciOiJSUzI1..."     │
│         ├── "repository:library/nginx:pull,push" → "eyJhbGciOiJS..."    │
│         └── "repository:myapp:pull" → "eyJhbGciOiJSUzI1NiIsInR5..."     │
├─────────────────────────────────────────────────────────────────────────┤
│ Key: "ORAS_AUTH_ghcr.io"                                                │
│ Value: CacheEntry                                                       │
│   ├── Scheme: Bearer                                                    │
│   └── Tokens: Dictionary<string, string>                                │
│         └── "repository:myorg/myimage:pull,push" → "gho_xxxx..."        │
└─────────────────────────────────────────────────────────────────────────┘
```

**Two-level keying:**
1. **Outer key (partition)**: Registry host (e.g., `docker.io`)
2. **Inner key (token lookup)**: OAuth2 scope string (e.g., `repository:library/nginx:pull`)

### 1.4 Current API Signatures

```csharp
public interface ICache
{
    bool TryGetScheme(string registry, out Challenge.Scheme scheme);
    void SetCache(string registry, Challenge.Scheme scheme, string key, string token);
    bool TryGetToken(string registry, Challenge.Scheme scheme, string key, out string token);
}

public interface ICredentialProvider
{
    Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken = default);
}
```

### 1.5 Current Auth Flow (Client.SendAsync)

```
1. Extract registry host from request URI
2. Check cache for existing scheme + token
3. If cache hit → attach token to request
4. If 401 Unauthorized → parse WWW-Authenticate challenge
5. Fetch new token (Basic or Bearer)
6. Cache token with key = registry host + scope string
7. Retry request with new token
```

---

## 2. Gap Analysis

### 2.1 Problem Statement

The current design assumes **one set of credentials per upstream registry**. This breaks down in multi-tenant scenarios:

**Scenario**: A service syncs container images from upstream registries (Docker Hub, MCR) to per-customer container registries.

```
Customer A: docker.io/library/nginx:latest → customerA.myregistry.example/nginx:latest
Customer B: docker.io/library/nginx:latest → customerB.myregistry.example/nginx:latest
Customer C: docker.io/library/nginx:1.25   → customerC.myregistry.example/nginx:1.25
```

Each customer may have:
- Different Docker Hub credentials (rate limit tokens, paid subscriptions)
- Different access policies (some can pull private images, others can't)
- Audit/compliance requirements for credential isolation

**Current behavior**: All three customers share the same cached token for `docker.io`, using whichever credentials were resolved first.

### 2.2 Specific Gaps

| Gap | Description | Impact |
|-----|-------------|--------|
| **Registry-only partitioning** | Cache key is just the host | Cannot isolate tokens by customer/tenant |
| **No context propagation** | `ICache` methods take only `string registry` | No way to pass additional context |
| **Credential provider limitation** | `ICredentialProvider` receives only hostname | Cannot resolve credentials based on downstream context |
| **Hardcoded key generation** | `GetCacheKey()` returns `$"{_cacheKeyPrefix}{registry}"` | No extension point for custom key logic |

### 2.3 Requirements for Solution

1. **Flexible partitioning**: Support cache keys at registry, repository, reference, or custom granularity
2. **User-controlled**: Let users decide the partitioning strategy
3. **Custom prefix support**: Allow implementation-specific identifiers (tenant ID, job ID)
4. **Backwards compatible**: Default behavior unchanged for existing users
5. **Composable**: Work with existing token/scope dictionary structure

---

## 3. Proposed Design (Simplified)

### 3.1 Design Principle

**Consumer-driven partitioning**: The SDK accepts a simple `tenantId` string. The consumer decides what this ID should be (hash of reference, customer ID, sync job ID, etc.). The SDK uses it as a prefix to partition the auth cache.

### 3.2 Cache Key Generation

```
Current:  ORAS_AUTH_{registry}
Proposed: ORAS_AUTH_{tenantId}|{registry}  (if tenantId provided)
          ORAS_AUTH_{registry}                 (if tenantId is null/empty)
```

### 3.3 API Changes

#### 3.3.1 RepositoryOptions (New Property)

```csharp
public class RepositoryOptions
{
    // ... existing properties ...

    /// <summary>
    /// Optional identifier for auth cache partitioning.
    /// When set, the auth cache key becomes "{tenantId}|{registry}".
    /// Use this to isolate cached tokens per customer, sync job, or any other
    /// partitioning strategy. The consumer is responsible for determining
    /// what value to use.
    /// </summary>
    public string? TenantId { get; set; }
}
```

#### 3.3.2 ICache (Updated Signatures)

```csharp
public interface ICache
{
    bool TryGetScheme(string registry, out Challenge.Scheme scheme, string? tenantId = null);

    void SetCache(string registry, Challenge.Scheme scheme, string scopeKey, string token,
        string? tenantId = null);

    bool TryGetToken(string registry, Challenge.Scheme scheme, string scopeKey, out string token,
        string? tenantId = null);
}
```

#### 3.3.3 Client (Updated Signature)

```csharp
public class Client
{
    // Updated signature with optional tenantId parameter
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string? tenantId = null,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default);
}
```

#### 3.3.4 Cache (Updated Implementation)

```csharp
public sealed class Cache : ICache
{
    private string GetCacheKey(string registry, string? tenantId) =>
        string.IsNullOrEmpty(tenantId)
            ? $"{_cacheKeyPrefix}{registry}"
            : $"{_cacheKeyPrefix}{tenantId}|{registry}";

    public bool TryGetScheme(string registry, out Challenge.Scheme scheme, string? tenantId = null)
    {
        var cacheKey = GetCacheKey(registry, tenantId);
        // ... rest unchanged
    }

    // Similar updates to SetCache and TryGetToken
}
```

### 3.4 Propagation Flow

```
User Code
    │
    │  var repo = new Repository(new RepositoryOptions {
    │      Reference = Reference.Parse("docker.io/library/nginx"),
    │      TenantId = "customer-123"
    │  });
    │
    ▼
Repository / BlobStore / ManifestStore
    │
    │  // Read TenantId from options, pass to Client
    │  await Client.SendAsync(request, tenantId: Options.TenantId, ct);
    │
    ▼
Client.SendAsync(request, tenantId, ct)
    │
    │  // Pass tenantId to cache operations
    │  Cache.TryGetScheme(host, out scheme, tenantId);
    │  Cache.SetCache(host, scheme, scopeKey, token, tenantId);
    │
    ▼
Cache
    │
    │  // Generate cache key with prefix
    │  key = "ORAS_AUTH_customer-123|docker.io"
    │
    ▼
IMemoryCache
```

### 3.5 Updated Cache Structure

With `tenantId = "customer-123"`:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ IMemoryCache                                                                │
├─────────────────────────────────────────────────────────────────────────────┤
│ Key: "ORAS_AUTH_customer-123|docker.io"                                     │
│ Value: CacheEntry                                                           │
│   ├── Scheme: Bearer                                                        │
│   └── Tokens: Dictionary<string, string>     ← UNCHANGED (inner structure) │
│         ├── "repository:library/nginx:pull" → "eyJhbG..."                   │
│         └── "repository:library/nginx:pull,push" → "eyJhbG..."              │
├─────────────────────────────────────────────────────────────────────────────┤
│ Key: "ORAS_AUTH_customer-456|docker.io"                                     │
│ Value: CacheEntry                                                           │
│   ├── Scheme: Bearer                                                        │
│   └── Tokens: Dictionary<string, string>                                    │
│         └── "repository:library/nginx:pull" → "eyJhbG_different..."         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Implementation Plan

### 4.1 Files to Modify

| File | Changes |
|------|---------|
| `RepositoryOptions.cs` | Add `TenantId` property |
| `ICache.cs` | Add optional `tenantId` parameter to all methods |
| `Cache.cs` | Update `GetCacheKey` to include `tenantId`; update method signatures |
| `Client.cs` | Update `SendAsync` signature with optional `tenantId`; pass to cache calls |
| `BlobStore.cs` | Pass `Options.TenantId` to `Client.SendAsync` |
| `ManifestStore.cs` | Pass `Options.TenantId` to `Client.SendAsync` |
| `Repository.cs` | Pass `Options.TenantId` to `Client.SendAsync` |
| `Registry.cs` | Pass `tenantId` if applicable |

### 4.2 Files to Remove (from previous implementation)

| File | Reason |
|------|--------|
| `AuthContext.cs` | Over-engineered; replaced by simple string |
| `CacheKeyGranularity.cs` | Consumer decides strategy, not SDK |
| `ICacheKeyGenerator.cs` | Not needed with simple string approach |
| `ScopedCacheKeyGenerator.cs` | Not needed with simple string approach |
| `DelegateCacheKeyGenerator.cs` | Not needed with simple string approach |

### 4.3 Implementation Phases

- **Phase 1: Core Changes**
  - Add `tenantId` property to `RepositoryOptions`
  - Update `ICache` interface with optional `tenantId` parameter
  - Update `Cache` implementation
  - Add `Client.SendAsync` overload with `tenantId`

- **Phase 2: Propagation**
  - Update `BlobStore` to pass `tenantId` to client
  - Update `ManifestStore` to pass `tenantId` to client
  - Update `Repository` to pass `tenantId` to client
  - Update `PlainClient` to implement new `IClient` interface

- **Phase 3: Cleanup**
  - Remove `AuthContext.cs`
  - Remove `CacheKeyGranularity.cs`
  - Remove `ICacheKeyGenerator.cs`
  - Remove `ScopedCacheKeyGenerator.cs`
  - Remove `DelegateCacheKeyGenerator.cs`
  - Update tests for removed types

- **Phase 4: Testing**
  - Update existing cache tests
  - Add multi-tenant partitioning tests (in CacheTest.cs)
  - Add backwards compatibility tests (null tenantId)
  - Update examples

- **Phase 5: Documentation**
  - Update XML docs
  - Update this plan document

---

## 5. Usage Examples

### 5.1 Default (Current Behavior - No Partitioning)

### 5.1 Default (Current Behavior - No Partitioning)
```csharp
var repo = new Repository(new RepositoryOptions
{
    Reference = Reference.Parse("docker.io/library/nginx:latest")
    // tenantId not set - uses registry-only cache key
});

await repo.Blobs.FetchAsync(descriptor, ct);
// Cache key: "ORAS_AUTH_docker.io"
```

### 5.2 Multi-Tenant with Customer ID

```csharp
// Customer A's sync job
var repoA = new Repository(new RepositoryOptions
{
    Reference = Reference.Parse("docker.io/library/nginx:latest"),
    TenantId = "customer-A"
});
await repoA.Blobs.FetchAsync(descriptor, ct);
// Cache key: "ORAS_AUTH_customer-A|docker.io"

// Customer B's sync job (same upstream, different cache partition)
var repoB = new Repository(new RepositoryOptions
{
    Reference = Reference.Parse("docker.io/library/nginx:latest"),
    TenantId = "customer-B"
});
await repoB.Blobs.FetchAsync(descriptor, ct);
// Cache key: "ORAS_AUTH_customer-B|docker.io"
```

### 5.3 Partitioning by Full Reference Hash

```csharp
var reference = Reference.Parse("docker.io/library/nginx:latest");
var tenantId = ComputeHash($"{reference.Registry}/{reference.Repository}:{reference.ContentReference}");

var repo = new Repository(new RepositoryOptions
{
    Reference = reference,
    TenantId = tenantId
});
// Cache key: "ORAS_AUTH_{hash}|docker.io"
```

### 5.4 Partitioning by Destination Reference

```csharp
// Sync: docker.io/library/nginx → customerA.myregistry.example/nginx
var destinationRef = "customerA.myregistry.example/nginx:latest";

var repo = new Repository(new RepositoryOptions
{
    Reference = Reference.Parse("docker.io/library/nginx:latest"),
    TenantId = destinationRef  // Use destination as partition key
});
// Cache key: "ORAS_AUTH_customerA.myregistry.example/nginx:latest|docker.io"
```

---

## 6. Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Simple string over complex types** | Consumer knows their partitioning needs; SDK shouldn't prescribe |
| **Property on RepositoryOptions** | Set once per Repository instance; avoids per-method parameters |
| **Optional parameter with null default** | Backwards compatible; existing code works unchanged |
| **Prefix before registry** | `{tenantId}|{registry}` keeps related entries grouped |
| **Inner cache structure unchanged** | OAuth2 scope-based token lookup still works |

---

## 7. Backwards Compatibility

- When `tenantId` is not used (`tenantId = null`), cache key behavior matches the previous version.
- Public interfaces such as `ICache`, `IClient`, and `Client.SendAsync` have been updated to
  accept an optional `tenantId`, which is a source-breaking change for consumers that implement
  or mock these interfaces.
- Call sites that use the default client implementations may require minimal or no changes,
  but interface implementers and tests must be updated to account for the new parameter.
- Runtime behavior for existing scenarios remains compatible when `tenantId` is left unset.

---

## 8. Open Questions (Resolved)

1. ~~Should `ICredentialProvider` also receive `tenantId` for context-aware credential resolution?~~
   **No.** Credential selection is a separate concern from token cache partitioning. Consumers can use different provider instances or implement tenant-aware logic internally.

2. ~~Should there be validation on `tenantId` (e.g., no colons allowed)?~~
   **No.** No validation required. The consumer is responsible for providing sensible values.

3. ~~Should `ScopeManager` also be partitioned by `tenantId`?~~
   **No.** ScopeManager will not be touched. It manages OAuth2 permission scopes, which is orthogonal to cache partitioning.
