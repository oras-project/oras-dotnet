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
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Tests.examples;

/// <summary>
/// Examples demonstrating multi-tenant auth cache partitioning.
/// 
/// These examples show how to configure the auth cache for scenarios where
/// different customers or tenants need isolated credential caching, even when
/// accessing the same upstream registries.
/// </summary>
public class MultiTenantAuthCacheExamples
{
    /// <summary>
    /// Example: Default behavior (registry-level partitioning).
    /// 
    /// This is the current/default behavior where tokens are cached per registry host.
    /// All requests to the same registry share the same cached tokens.
    /// </summary>
    public void DefaultRegistryPartitioning()
    {
        // Create repository with default settings (no tenantId)
        var reference = OrasProject.Oras.Registry.Reference.Parse(
            "myregistry.myregistry.example/library/nginx:latest");

        var options = new RepositoryOptions
        {
            Reference = reference,
            Client = new Client()
            // tenantId is null by default
        };

        var repository = new Repository(options);

        // All requests to this registry share the same cached tokens
        // Cache key: "ORAS_AUTH_myregistry.myregistry.example"
    }

    /// <summary>
    /// Example: Multi-tenant with customer-specific tenantId.
    /// 
    /// Each customer gets isolated cache entries by setting tenantId on RepositoryOptions.
    /// This is ideal for services that sync content for multiple customers.
    /// </summary>
    public void MultiTenantWithCustomerId()
    {
        var reference = OrasProject.Oras.Registry.Reference.Parse(
            "docker.io/library/nginx:latest");

        // Customer A's repository - uses customer ID as partition key
        var customerAOptions = new RepositoryOptions
        {
            Reference = reference,
            Client = new Client(),
            TenantId = "customer-A"
        };
        var customerARepo = new Repository(customerAOptions);
        // Cache key: "ORAS_AUTH_customer-A|docker.io"

        // Customer B's repository - same registry, different customer
        var customerBOptions = new RepositoryOptions
        {
            Reference = reference,
            Client = new Client(),
            TenantId = "customer-B"
        };
        var customerBRepo = new Repository(customerBOptions);
        // Cache key: "ORAS_AUTH_customer-B|docker.io"

        // Tokens are completely isolated between customers
    }

    /// <summary>
    /// Example: Sync service scenario using destination reference as partition key.
    /// 
    /// A service syncing images from upstream registries to per-customer ACRs.
    /// Each sync operation uses the destination reference as the tenantId.
    /// </summary>
    public void SyncServiceScenario()
    {
        // Syncing docker.io/library/nginx:latest -> customerA.myregistry.example/nginx:latest
        var sourceRef = OrasProject.Oras.Registry.Reference.Parse(
            "docker.io/library/nginx:latest");
        var destinationRef = "customerA.myregistry.example/nginx:latest";

        var options = new RepositoryOptions
        {
            Reference = sourceRef,
            Client = new Client(),
            // Use destination reference as partition key
            TenantId = destinationRef
        };
        var sourceRepo = new Repository(options);
        // Cache key: "ORAS_AUTH_customerA.myregistry.example/nginx:latest:docker.io"

        // Syncing same upstream to different customer
        var destinationRef2 = "customerB.myregistry.example/nginx:latest";
        var options2 = new RepositoryOptions
        {
            Reference = sourceRef,
            Client = new Client(),
            TenantId = destinationRef2
        };
        var sourceRepo2 = new Repository(options2);
        // Cache key: "ORAS_AUTH_customerB.myregistry.example/nginx:latest:docker.io"
        // Different cache entry, potentially different upstream credentials
    }

    /// <summary>
    /// Example: Environment-based partitioning.
    /// 
    /// Partition cache by environment (dev/staging/prod) combined with customer ID.
    /// </summary>
    public void EnvironmentBasedPartitioning()
    {
        var environment = Environment.GetEnvironmentVariable("APP_ENV") ?? "development";
        var customerId = "customer-123";

        var reference = OrasProject.Oras.Registry.Reference.Parse(
            "docker.io/library/nginx:latest");

        var options = new RepositoryOptions
        {
            Reference = reference,
            Client = new Client(),
            TenantId = $"{environment}:{customerId}"
        };

        var repo = new Repository(options);
        // Cache key: "ORAS_AUTH_development:customer-123:docker.io"
    }

    /// <summary>
    /// Example: Direct cache API usage with tenantId.
    /// 
    /// Shows how tenantId is used directly at the cache level.
    /// </summary>
    public void DirectCacheUsage()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var cache = new Cache(memoryCache);

        var registry = "docker.io";
        var scopeKey = "repository:library/nginx:pull";

        // Set tokens for different customers
        cache.SetCache(registry, Challenge.Scheme.Bearer, scopeKey, "token-for-customer-A", "customer-A");
        cache.SetCache(registry, Challenge.Scheme.Bearer, scopeKey, "token-for-customer-B", "customer-B");

        // Each customer has isolated token cache
        cache.TryGetToken(registry, Challenge.Scheme.Bearer, scopeKey, out var tokenA, "customer-A");
        cache.TryGetToken(registry, Challenge.Scheme.Bearer, scopeKey, out var tokenB, "customer-B");

        // tokenA == "token-for-customer-A"
        // tokenB == "token-for-customer-B"
    }
}
