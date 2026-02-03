# GitHub Copilot Repository Instructions

Concise guidance for producing code and docs consistent with the ORAS .NET library. Keep signal high, duplication low.

## Project Snapshot
- Target: .NET 8 (`net8.0`), nullable enabled, warnings are errors.
- Analyzer: `Microsoft.CodeAnalysis.NetAnalyzers` (latest).
- Tests: xUnit + Moq.
- Package: NuGet generated on build.
- Core domain: OCI descriptors, manifests (image + generic artifacts), blobs, registries, push / fetch / copy, referrers.

## Spec & Lineage
- Origin: .NET-native redesign inspired by [oras-go v2](https://github.com/opencontainers/oras-go) ([pkg docs](https://pkg.go.dev/oras.land/oras-go/v2)). Not a mechanical port.
- Goal: Provide idiomatic C# (async/await, explicit interfaces, POCO models, typed exceptions) while preserving semantics of OCI artifacts operations.
- Current Target Specs (v1.1.1):
	- OCI Image Spec – https://github.com/opencontainers/image-spec/tree/v1.1.1
	- OCI Distribution Spec – https://github.com/opencontainers/distribution-spec/tree/v1.1.1
- Evolution: Prefer additive changes; open follow-up issues referencing spec deltas when specs advance. Avoid breaking public APIs outside a new major version.
- Normative Behavior: Descriptor / Manifest / Index handling must follow MUST / MUST NOT statements from specs. When ambiguity exists, mirror oras-go behavior unless it conflicts with .NET idioms or analyzers.
- Design Translation Principles:
	- Go `context.Context` -> trailing `CancellationToken` (defaultable) in public async APIs.
	- Error returns -> typed exceptions (e.g., `NotFoundException`).
	- Unprefixed Go interfaces -> `I`-prefixed C# interfaces.
	- Streams over byte[]: use `Stream` + spans internally to minimize allocations.
	- Functional options -> explicit option classes (e.g., `CopyOptions`).
	- Only async I/O; add sync wrapper only if unavoidable.
- Spec Linkage in Code: Public members that directly model spec structures (e.g., `Descriptor`, `Manifest`) should include an XML doc link to the relevant spec section.
- Validation Priority: Enforce digest + size integrity, required media type presence, and optional `artifactType` non-empty if set before storing/publishing.
- Compatibility: Keep JSON property names & omission behavior aligned with spec (use conditional ignore attributes rather than post-serialization filtering).
- Error Mapping: HTTP 4xx -> specific domain exceptions when meaningful; 5xx -> surfaced with context (do not over-wrap). Digest mismatch always hard-fails.
- Non-Goals: Do not expose raw HTTP plumbing unless required for advanced extensibility; abstractions added only after repeated need.

## Go → .NET Guideline Quick Map
| Go Concept             | Here                                                    |
| ---------------------- | ------------------------------------------------------- |
| `context.Context`      | Final `CancellationToken cancellationToken = default`   |
| `(T, error)`           | `Task<T>` + typed exceptions                            |
| Interfaces (no prefix) | Prefix with `I`                                         |
| `io.Reader` / `Writer` | `Stream` async APIs                                     |
| `[]byte`               | `ReadOnlyMemory<byte>` / `Span<byte>` internally        |
| Functional options     | Option classes (e.g., `CopyOptions`)                    |
| Package funcs          | Static helpers / focused types                          |
| Sync + Async           | Async only for I/O (add sync wrapper only if essential) |

## Adding / Changing Features (Checklist)
1. Link relevant spec section in XML docs.
2. Ensure media type constant exists (under `Oci.MediaType` / `Docker.MediaType`).
3. Validate digest + size immediately; fail fast on mismatch.
4. JSON shape: correct names, optional vs required, omission rules.
5. HTTP interactions: correct method + status handling; map 4xx to specific exceptions; surface 5xx with context.
6. Tests: include at least one normative MUST scenario + negative case.

## Core Concepts (Essentials)
- Artifact: Any OCI object (image, signature, SBOM, provenance, etc.). Identity = descriptor (media type + digest + size). Avoid image-only assumptions.
- Referrers: Manifests with a `subject` linking to another digest. Validate subject digest, media type, and size.
- References: Accept tags, normalize early to digest; propagate descriptors internally. Distinguish parse errors vs. not found via specific exceptions.
- Media vs Artifact Type: Media type drives processing; `artifactType` is optional metadata—preserve if provided (validate non-empty string if present).

## CAS (Content Addressable Storage) Rules
- Key = digest (e.g., `sha256:<hex>`). Immutability is strict.
- Ingest: stream + hash -> compare declared (size + digest) -> store or throw.
- Duplicate push is a no-op.
- Tags are pointers; resolve to digest ASAP.
- Maintain clean separation of tag -> digest indices; no silent rewrites.
- Tests should cover: duplicate push, digest mismatch, tag -> digest resolution.

## Coding Style & Architecture
- File-scoped namespaces: `namespace OrasProject.Oras.<Area>;`
- Explicit access modifiers; small, single-responsibility types.
- Public API: minimal, purposeful. Internal helpers remain internal.
- Use `required` properties when logically mandatory.
- Avoid speculative abstractions & unnecessary dependencies.
- **Line length: hard limit 120 columns; target ~100 for readability.**
  Break long method signatures, chained calls, or expressions across lines.

## Models & Serialization
- Use `System.Text.Json` with `[JsonPropertyName]` / conditional ignore attributes.
- Keep value objects immutable; provide factory methods for validation (e.g., `Descriptor.Create`).

## Exceptions & Validation
- Prefer existing types in `OrasProject.Oras.Exceptions` (e.g., `NotFoundException`, `InvalidMediaTypeException`).
- Introduce new exception types only for clearly distinct error categories.
- Validate early: digests, media types, sizes, artifactType (non-empty if set).

## Async & I/O
- Async-first (`Async` suffix). No blocking `.Result` / `.Wait()`.
- `CancellationToken` always last with default.

## Testing
- Mirror source folder structure under `tests/OrasProject.Oras.Tests/`.
- Prefer real in-memory stores (e.g., `MemoryStore`) over mocks when feasible.
- Each feature: happy path + invalid input + edge case + spec MUST assertion.
- Interface changes: include at least one test using the interface type
  (e.g., `IRepository repo = ...`) to verify polymorphic usage and DI scenarios.
- Keep tests focused; parameterize when it aids clarity.

## Performance
- Stream large payloads; avoid full buffering.
- Minimize allocations (use spans / memory where appropriate).
- Defer expensive computation until needed.

## Public API Change Gate
Before merging a public change confirm: naming OK, XML docs + spec link, tests updated, non-breaking (or documented), nullability accurate, **line lengths ≤100 columns (hard limit 120)**.

## Security Considerations
- Treat digest mismatch as tamper -> throw.
- Validate external inputs rigorously; never mutate stored bytes.
- Avoid leaking internal exception implementation details across boundaries.

## Dependency & Analyzer Policy
- Keep analyzer warnings at zero (warnings = errors).
- Avoid new external dependencies unless essential & justified.

## PR Guidance
- One logical change per PR, with summary + rationale + breaking change note (if any).
- All tests must pass; add coverage for new logic.
- **Verify line lengths:** new/modified lines should be ≤100 columns (hard limit 120).
  Run a length check before finalizing review.

## Copilot Prompt Hints
Clarify intent in comments, e.g.:
// Goal: Add async method to fetch manifest by digest.
// Test: Verify NotFoundException on missing blob.

## Non-Goals
- No speculative features unrelated to OCI registry operations.
- No large refactors without explicit request.
- No convenience dependencies just to simplify short code.

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
Update this file as conventions evolve; keep it succinct.
