# ORAS Authentication Model

## Abstract

This design document outlines the initial approach for implementing bearer token authentication in ORAS, as well as the authentication workflow between the registry and the authorization server. Bearer token authentication is preferred over basic authentication because it offers better security, scalability, and flexibility, allowing for smoother integration with modern authentication protocols and reducing the risk of exposing sensitive credentials. Supporting bearer token authentication aligns with OAuth2 standards, which are widely adopted by modern registries, providing a secure and efficient way to authenticate users and manage access.

## Introduction

Currently, the ORAS .NET SDK does not fully implement an authentication model for registries. This design document outlines the high-level design for implementing bearer token authentication, enabling support for secure user authentication, and detailing the authentication workflow between the registry and the authorization server.


## Design

### Auth Client Design:

In this design, the Client class, which inherits from HttpClient, is structured with several key variables: ForceAttemptOAuth2, ClientId, Cache, Credential, and ScopeManager.

- **ICredentialHelper**: An interface that defines a Resolve method, which must be implemented by the user. This approach provides flexibility and extendability, allowing for seamless integration with different cloud providers.

- **Cache**: This component is responsible for storing the access token retrieved from the authorization server, specifically for each repository, to optimize repeated token requests.

- **ScopeManager** is responsible for managing scopes for the currently instantiated Client. By default, a ScopeManager is automatically created during Client instantiation. However, Users can also provide a ScopeManager if desired.

- **ForceAttemptOAuth2**: This variable acts as a toggle to enable or disable OAuth2 authentication, giving the user control over the authentication method.

- **ClientId**: The ClientId is the identifier used when sending requests to the registries.

```mermaid
  erDiagram

    HttpClient {}

    Client {
        boolean ForceAttemptOAuth2
        string ClientId
        Cache Cache
        ICredentialHelper Credential
        ScopeManager ScopeManager
    }
    
    ICredentialHelper {
        Task(Credential) Resolve(registry)
    }
    
    ScopeManager {
        ConcurrentDictionary Scopes
    }

    Scope {
        string ResourceType
        string ResourceName
        hashset actions
    }

    Credential {
        string username
        string password
        string refreshtoken
        string accessToken
    }

    Cache {
        ConcurrentDictionary(CacheEntry) _caches
    }

    CacheEntry {
        Scheme Scheme
        Dictionary Tokens
    }

    Challenge {
    }

    Scheme {
        string Basic "Basic authentication scheme"
        string Bearer "Bearer token authentication scheme"
        string Unknown "Unknown or unsupported authentication scheme"
    }

    HttpClient ||--o| Client : inherits
    Client ||--o| Cache : contains
    Client ||--o| ICredentialHelper : contains
    Client ||--o| ScopeManager : contains
    ScopeManager ||--o| Scope : manages
    Cache ||--o| CacheEntry : contains
    Challenge ||--o| Scheme : contains
    
    Client ||--o| SendAsync : overrides
    Client ||--o| FetchBasicAuth : method
    Client ||--o| FetchBearerAuth : method
    ScopeManager ||--o| GetScopesStringForHost : method
    ScopeManager ||--o| GetScopesForHost : method
    ScopeManager ||--o| SetActionsForRepository : method
    ScopeManager ||--o| SetScopeForRegistry : method
    ICredentialHelper ||--o| Credential : returns
    Challenge ||--o| ParseChallenge : method
```

### Authentication Workflow:

```mermaid
sequenceDiagram
  participant Client as ORAS Client
  participant Registry as OCI Registry
  participant Auth as Authorization Service

  Client->>Registry: 1. Begin push/pull operation
  Registry-->>Client: 2. 401 Unauthorized (Www-Authenticate header)
  Client->>Auth: 3. Request Bearer token
  Auth-->>Client: 4. Return Bearer token
  Client->>Registry: 5. Retry with Bearer token
  Registry-->>Client: 6. Authorize and begin session
```

1. ORAS client attempts to begin a push/pull operation with the registry.
2. If the registry requires authorization, it will return a 401 Unauthorized HTTP response with information, i.e. Www-Authenticate header, on how to authenticate, Ref: https://datatracker.ietf.org/doc/html/rfc7235#section-2.1
3. The ORAS client makes a request to the authorization service for a Bearer token.
4. The authorization service returns an opaque Bearer token representing the client’s authorized access.
5. The ORAS client retries the original request with the Bearer token embedded in the request’s Authorization header.
6. The Registry authorizes the client by validating the Bearer token and the claim set embedded within it and begins the push/pull session as usual.


# Considerations
1. The auth model provides a interface ICredentialHelper for the users to retrieve the credentials from different registries and return it back to ORAS to process it.
2. To handle refresh token expiry, users can refresh tokens if the request returns 401 Unauthorized
3. To handle access token expiry, ORAS SDK does have implemented retry logic for the cached access token just in case it is expired.
