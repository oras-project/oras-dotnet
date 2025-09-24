# GitHub Copilot Repository Instructions

These guidelines help Copilot (and contributors) produce code and docs consistent with the ORAS .NET library.

## Project Overview
- Target Framework: .NET 8 (`net8.0`)
- Nullable: Enabled
- Warnings: Treated as errors (`TreatWarningsAsErrors=true`)
- Analyzers: `Microsoft.CodeAnalysis.NetAnalyzers` (AnalysisLevel=latest)
- Test Framework: xUnit + Moq
- Packaging: Library produces a NuGet package on build (`GeneratePackageOnBuild=true`).
- Domain Concepts: OCI descriptors, manifests, blobs, registries, artifact push/fetch/copy.

## Lineage & Spec Compliance
- Origin: This SDK is a .NET-native redesign inspired by the Go implementation: [oras-go v2](https://github.com/oras-project/oras-go) / [pkg docs](https://pkg.go.dev/oras.land/oras-go/v2).
- Goal: Provide idiomatic C# APIs (async/await, `I*` interfaces, POCO models) rather than a 1:1 mechanical port from Go.
- Target Specs (CURRENT):
    - OCI Image Spec: v1.1.1 – https://github.com/opencontainers/image-spec/tree/v1.1.1
    - OCI Distribution Spec: v1.1.1 – https://github.com/opencontainers/distribution-spec/tree/v1.1.1
- When specs update: Prefer additive evolution; create follow-up issues referencing spec section deltas. Avoid breaking API changes unless moving to a new MAJOR version.
- Descriptor / Manifest / Index behavior should match normative MUST / MUST NOT statements from the specs. Where ambiguity exists, mirror oras-go v2 semantics unless they conflict with .NET idioms or analyzer warnings.

### Translating Go Concepts to .NET
| Go Pattern (oras-go v2)       | .NET Guidance Here                                                                           |
| ----------------------------- | -------------------------------------------------------------------------------------------- |
| `context.Context` param first | Use trailing `CancellationToken cancellationToken = default`                                 |
| Error returns `(T, error)`    | Return `T` (or `Task<T>`) and throw specific exceptions (e.g., `NotFoundException`)          |
| Interfaces without `I` prefix | Prefix with `I` (e.g., `Target` -> `ITarget`)                                                |
| `io.Reader` / `io.Writer`     | Use `Stream` / `Stream`-based helpers, favor async (`ReadAsync`, etc.)                       |
| Slices `[]byte`               | Prefer `ReadOnlyMemory<byte>` / `Span<byte>` internally; expose `byte[]` only when necessary |
| Functional options pattern    | Use option classes (e.g., `CopyOptions`) with required/optional properties                   |
| Package-level funcs           | Prefer static helper classes or instance methods tied to a responsibility                    |
| Combined sync+async APIs      | Provide only async for I/O; add sync wrapper ONLY if unavoidable                             |

### Spec Mapping Checklist (When Adding Features)
1. Identify relevant spec section (link it in XML docs summary or remarks).
2. Confirm media type constants exist (under `Oci.MediaType` or `Docker.MediaType`). Add if missing.
3. Validate digest + size invariants early (throw on mismatch rather than defer).
4. Ensure JSON shape matches spec (property names, optional vs required, omission rules via `JsonIgnore`).
5. For registry interactions, confirm HTTP method, expected status codes, and error translation (4xx -> specific exception where meaningful; 5xx -> rethrow with context).
6. Add at least one test mirroring a normative MUST requirement.

### Avoid Direct Copy of Go Naming
Do NOT:
- Keep snake_case or lowercase exported Go names.
- Port unneeded concurrency primitives (channels, contexts) – rely on .NET async/await and cancellation tokens.
- Expose raw HTTP internals unless required for advanced scenarios (wrap or abstract later if recurring).

### Example Port (Conceptual)
Go (conceptual):
```go
desc, err := content.Fetch(ctx, ref)
if errors.Is(err, ErrNotFound) { ... }
```
Idiomatic .NET here:
```csharp
try
{
        var descriptor = await target.FetchAsync(reference, cancellationToken);
}
catch (NotFoundException ex)
{
        // handle missing blob
}
```
Rationale: explicit exception types integrate better with .NET tooling, simplify call-site branching, and align with existing exception taxonomy in `Exceptions/`.

## Core ORAS Concepts (Quick Reference)
Authoritative docs:
- Artifact: https://oras.land/docs/concepts/artifact
- Reference Types (Referrers): https://oras.land/docs/concepts/reftypes
- Reference (Name / Tag / Digest forms): https://oras.land/docs/concepts/reference

Summaries (for contributors – keep code + tests aligned with these semantics):

### Artifact
An "artifact" is any OCI object (image, signature, SBOM, provenance, scan report, etc.) identified by a manifest (or index) plus its associated blobs. It is NOT limited to container images. Our APIs should:
- Treat media type + digest (descriptor) as the canonical identity.
- Permit attaching arbitrary `annotations` and optional `artifactType`.
- Avoid image-specific assumptions (e.g., config layers) unless explicitly working with image media types.

### Reference Types (Referrers)
Reference Types add relationships between artifacts (e.g., a signature referring to a subject). A "referrer" manifest includes a `subject` field pointing to another manifest's digest. Implementation guidance:
- Provide listing/filtering of referrers by subject descriptor.
- Preserve ordering only if semantically needed; otherwise return stable deterministic order (e.g., by digest) for testability.
- Enforce that a referrer must not mutate the subject content; only establish linkage.
- Validate subject digest & media type before persisting/publishing.

### Reference (Names, Tags, Digests)
Registry references come in three principal forms:
- `repository:tag` (mutable reference – may move over time)
- `repository@digest` (immutable content-addressable reference)
- `registry/namespace/repository[:tag|@digest]` (fully qualified)
Guidelines:
- Prefer digest form internally for idempotency and cache keys.
- Accept tag input at user boundaries; resolve to digest early and propagate descriptor objects through internal methods.
- When returning references, include both tag and digest if both are known (e.g., after resolving a tag lookup) to reduce duplicate remote calls.
- Distinguish parsing errors (invalid syntax) from not found (valid reference, absent content) using specific exceptions where appropriate (`NotFoundException`, `InvalidMediaTypeException`, etc.).

### Practical Usage Patterns
- For copy/push: normalize to descriptor (digest) first, then operate; expose tag assignment as a discrete step to avoid ambiguity.
- For referrers APIs: always require the subject descriptor, not just a tag.
- For tests: include at least one scenario per feature using a non-image artifact type to ensure generality.

### Media Type & Artifact Type Notes
- Media type drives processing rules (manifest vs index vs blob).
- `artifactType` is optional metadata for distinguishing higher-level semantics (e.g., SPDX, signature) and should be preserved verbatim (validate non-empty if provided).

### What NOT to Assume
- That every manifest has a config (non-image artifacts may not).
- That layer ordering implies execution order (only relevant for runtime image semantics, which are out-of-scope for generic artifacts).
- That tags are stable – never cache tag -> digest without an expiry strategy (if adding caching later).

### Content Addressable Storage (CAS)
CAS is foundational: every stored object (manifest, index, blob) is addressed and deduplicated by its digest (algorithm + hex). Design & implementation guidance:
- Canonical Key: `sha256:<hex>` (or other supported algorithm). Never rely on filenames, upload order, or tags for identity.
- Immutability: Once a digest is persisted, its byte content MUST NOT change. Mutations create a new digest.
- Validation Path: On ingest (push / write):
    1. Compute digest while streaming (avoid double buffering where possible).
    2. Compare computed digest & size with declared descriptor before finalizing.
    3. Reject (throw) if mismatch (`SizeLimitExceededException`, custom digest exception) – fail fast.
- Idempotency: A push of existing digest should be a no-op (or short-circuited) – callers may optimistically attempt duplicate writes.
- Tags vs Digests: A tag is a mutable pointer; a digest is immutable content identity. Always resolve tags to a digest early, propagate descriptors internally.
- Local OCI Layout: Mirrors registry addressing on disk (e.g., `blobs/sha256/<hex>` plus `index.json`). Ensure any future filesystem store adheres to layout spec so content can be moved between local + remote seamlessly.
- Memory / File Stores: Use digest as the dictionary key; optionally maintain secondary indices (e.g., tag -> digest) with clear invalidation semantics.
- Garbage Collection: Safe GC requires reachability analysis from root references (tags, explicitly pinned digests, referrers). Do not implement destructive cleanup without a graph traversal (see `MemoryGraph` for in-memory modeling).
- Security Considerations: Any digest mismatch indicates tampering/corruption; treat as hard failure. Never auto-correct or silently rewrite descriptors.
- Descriptor Authoring: When creating a `Descriptor` from raw data (`Descriptor.Create`), size & digest are authoritative; callers must supply correct `MediaType` and optional `ArtifactType`.
- Streaming Uploads: Favor a hashing stream wrapper to compute digest inline; avoid loading entire large blobs (future optimization hook).

Keep CAS assumptions explicit in tests: include at least one duplicate push, one digest mismatch scenario, and a tag resolution -> digest path.

## General Coding Style
- Use `namespace OrasProject.Oras.<Area>;` file-scoped namespaces (as in existing code) when applicable.
- Prefer explicit access modifiers.
- Keep classes small and focused on a single responsibility (content storage, OCI descriptors, registry interaction, etc.).
- Use `required` init/set properties where logically mandatory (mirroring existing DTO patterns like `Descriptor`).
- Avoid prematurely adding abstractions or interfaces unless there's a clear extensibility point already present in the codebase.
- Do not add external dependencies unless essential; prefer BCL / existing references.
- Keep public API surface minimal and purposeful; internal helpers should be `internal`.

## Documentation & Comments
- Include the standard Apache 2.0 license header at the top of all new source files.
- Use XML documentation (`/// <summary>`) for all public types and members.
- Link to relevant OCI spec sections where helpful (e.g., descriptor, manifest, media type specs).
- Keep summaries concise; avoid duplicating obvious information.

## Error Handling & Exceptions
- Use existing exception types under `OrasProject.Oras.Exceptions` when applicable.
- Introduce new exceptions only if a distinct error category is needed.
- Prefer throwing the most specific exception with a clear message; avoid swallowing exceptions silently.

## Serialization & Models
- Use `System.Text.Json` attributes (`[JsonPropertyName]`, `JsonIgnoreCondition.WhenWritingDefault`) as shown in existing models.
- Maintain immutability for value objects where possible; provide factory methods like `Descriptor.Create` when appropriate.
- Validate critical fields early (e.g., digest/media type non-empty) with guard clauses or helper validators.

## Digest & Content Operations
- Use existing `Digest` utilities; do not re-implement hashing logic.
- When computing digests, operate on byte arrays; avoid multiple copies when not needed.

## Async & I/O
- Prefer async methods for network/registry interactions; name with `Async` suffix.
- Avoid blocking calls (`.Result`, `.Wait()`).
- Use `CancellationToken` for public async APIs that may perform I/O.

## Testing Guidelines
- Place new tests under `tests/OrasProject.Oras.Tests/` mirroring the namespace and folder structure of the code under test.
- Use xUnit `[Fact]` for single-scenario tests and `[Theory]` with `[InlineData]` for parameterized scenarios.
- Keep assertions focused (Arrange / Act / Assert pattern) and avoid testing multiple unrelated behaviors in one test.
- Mock only external or slow dependencies (Moq). Prefer real in-memory implementations where available (e.g., `MemoryStore`).
- Provide at least: happy path, invalid input, and edge case tests.

## Performance & Memory
- Avoid unnecessary allocations (prefer spans where applicable; only materialize arrays when required by APIs).
- Stream large payloads; do not buffer entire blobs unless necessary.
- Defer expensive operations until actually required (lazy evaluation) if it improves performance meaningfully.

## Public API Additions Checklist
When adding/changing public APIs:
1. Verify naming aligns with existing terminology (push, fetch, copy, manifest, descriptor, tag, blob).
2. Add XML docs and (if applicable) link to OCI spec.
3. Add/adjust unit tests.
4. Consider versioning impact (breaking change risk). Maintain backwards compatibility unless explicitly coordinated.
5. Ensure nullability annotations are correct.

## Prompt Patterns for Copilot
When prompting for code completions, prefer explicit context:
```
// Goal: Add an async method to fetch a manifest by digest with cancellation support.
```
Or for a test:
```
// Test: Verify NotFoundException is thrown when fetching a non-existent blob.
```

## Example: Adding a New Model
If introducing a lightweight model corresponding to an OCI concept:
- Use `public class` with `[JsonPropertyName]` attributes.
- Provide a static factory method if creation requires validation or derived field computation.
- Keep optional fields nullable with conditional ignore attributes.

## Example: Adding Registry Interaction Method
- Add interface method under appropriate `Registry` interface only if it is logically universal.
- Otherwise, add an extension method (see existing `TargetExtensions`) for composability.
- Return Task-based APIs; include `CancellationToken` last.

## Logging
- The current project does not include a logging abstraction; avoid introducing one unless agreed upon. Use exceptions for error signaling.

## File & Folder Conventions
- Organize by domain (Content, Oci, Registry, Docker, Exceptions).
- Tests mirror the same folder structure.
- Keep `Remote` or protocol-specific code under `Registry/Remote`.

## Code Quality & Analyzers
- Fix all analyzer warnings (they are treated as errors in CI).
- Avoid suppressing warnings unless there's a strong justification; document with a comment.

## Dependency Management
- Keep versions consistent with existing packages; upgrade in coordinated PRs.
- Avoid transient dependency bloat.

## Security Considerations
- Validate external inputs (digests, media types, sizes, references) before processing.
- Avoid leaking internal exceptions across abstraction boundaries—wrap when appropriate.

## Example High-Quality Test Skeleton
```csharp
[Fact]
public async Task FetchManifestAsync_ThrowsNotFound_ForUnknownDigest()
{
    // Arrange
    var store = new MemoryStore();
    var target = /* create target using store */;
    var digest = "sha256:deadbeef...";

    // Act & Assert
    await Assert.ThrowsAsync<NotFoundException>(() => target.FetchManifestAsync(digest, CancellationToken.None));
}
```
(Replace stubbed pieces with actual constructs available in the codebase.)

## Commit & PR Guidance (For AI-Generated Changes)
- Keep PRs focused (one feature/fix).
- Include a clear summary and rationale.
- Note any potential breaking changes explicitly.
- Ensure all tests pass and add new tests for new logic.

## Non-Goals for Copilot
Copilot should NOT:
- Introduce speculative features unrelated to OCI registry operations.
- Add new external dependencies for convenience only.
- Generate large refactors without explicit instruction.

## Lightweight Prompt Examples
- "Add XML docs for Descriptor.BasicDescriptor property." 
- "Create NotFoundException test verifying message preservation." 
- "Add CancellationToken to existing async fetch method (non-breaking)."

## License Header Template
```
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
```

---
These instructions are meant to guide AI-assisted contributions to stay consistent, secure, and maintainable. Update this file as conventions evolve.
