# Realm Validation

When a registry responds to a request with a `401 Unauthorized` and a
`WWW-Authenticate: Bearer` challenge, that challenge contains a `realm` — the URL
of the token endpoint the client should contact to obtain a bearer token. The
`realm` is **supplied by the (potentially untrusted) registry**, so before the
client sends credentials there, it validates the realm with an
[`IRealmValidator`](../../src/OrasProject.Oras/Registry/Remote/Auth/IRealmValidator.cs).

This guards against a malicious or compromised registry directing the client to
send credentials/tokens to an attacker-controlled host.

## Default behavior: least-permissive

`Client` uses [`DefaultRealmValidator`](../../src/OrasProject.Oras/Registry/Remote/Auth/DefaultRealmValidator.cs)
unless you supply your own. It applies these rules, in order:

1. Reject non-HTTP(S) schemes.
2. Reject `http` unless `AllowInsecureHttp` is enabled.
3. Reject realm URLs containing userinfo (e.g. `https://user:pass@host/...`).
4. **Allow** if the realm host matches the registry host (same host, port-aware).
5. **Allow** if the realm host is in `TrustedRealmHosts`.
6. Otherwise **deny**.

> [!IMPORTANT]
> `TrustedRealmHosts` is **empty by default**. Out of the box, only realms on the
> **same host** as the registry are allowed (rule 4). The SDK is intentionally
> host-agnostic and does **not** bake in trust for any specific registry.

Many public registries serve their token endpoint from a **different** host than
the registry itself, for example:

| Registry            | Auth realm host     |
| ------------------- | ------------------- |
| `registry-1.docker.io` (Docker Hub) | `auth.docker.io`    |
| `registry.gitlab.com` (GitLab)      | `gitlab.com`        |
| `nvcr.io` (NVIDIA NGC)              | `authn.nvidia.com`  |

Because these auth hosts differ from the registry host, they are **rejected by
default** and must be opted into explicitly (see below).

## Opting in to cross-host auth realms

Add the realm's auth host(s) to `TrustedRealmHosts`:

```csharp
using OrasProject.Oras.Registry.Remote.Auth;

var validator = new DefaultRealmValidator
{
    TrustedRealmHosts = new HashSet<string>
    {
        "auth.docker.io",   // Docker Hub (registry-1.docker.io)
        "gitlab.com",       // GitLab (registry.gitlab.com)
        "authn.nvidia.com", // NVIDIA NGC (nvcr.io)
        "login.mycompany.com", // your enterprise auth endpoint
    }
};

var client = new Client(httpClient)
{
    RealmValidator = validator
};
```

Hostnames are normalized (lowercased, trailing dots stripped) and the match is
case-insensitive. A trusted host is still required to be on the default port for
its scheme and to pass the scheme/userinfo checks above.

## Local development over HTTP

For a registry running on `localhost` without TLS, enable `AllowInsecureHttp`:

```csharp
var validator = new DefaultRealmValidator { AllowInsecureHttp = true };
var client = new Client(httpClient) { RealmValidator = validator };
```

This only relaxes the scheme check; the same-host / trusted-host rules still apply.

## Custom validators

For full control (for example, allowing any auth host within a corporate domain
suffix), implement `IRealmValidator` and assign it to `Client.RealmValidator`:

```csharp
public class CorporateDomainRealmValidator : IRealmValidator
{
    private readonly string _allowedSuffix;

    public CorporateDomainRealmValidator(string allowedDomainSuffix)
        => _allowedSuffix = allowedDomainSuffix.ToLowerInvariant();

    public Task<bool> IsRealmAllowedAsync(
        Uri registryUri, Uri realmUri, CancellationToken cancellationToken = default)
    {
        if (!realmUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var host = realmUri.Host.ToLowerInvariant();
        return Task.FromResult(host.EndsWith(_allowedSuffix, StringComparison.Ordinal));
    }
}
```

## Runnable examples

See
[`examples/OrasProject.Oras.Examples.RealmValidation`](../../examples/OrasProject.Oras.Examples.RealmValidation/RealmValidationExamples.cs)
for runnable versions of the snippets above, including
`CreateClientTrustingWellKnownPublicRegistries`.
