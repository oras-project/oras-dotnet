# GitHub Copilot Repository Instructions

ORAS .NET library conventions. Signal over prose.

## Project
- .NET 8 (`net8.0`), nullable, warnings-as-errors.
- Analyzer: `Microsoft.CodeAnalysis.NetAnalyzers`.
- Tests: xUnit + Moq. Package: NuGet on build.
- Domain: OCI descriptors, manifests, blobs, registries, push/fetch/copy, referrers.

## Spec & Lineage
- Idiomatic C# redesign of [oras-go v2](https://github.com/opencontainers/oras-go). Not a port.
- Specs (v1.1.1): [Image](https://github.com/opencontainers/image-spec/tree/v1.1.1), [Distribution](https://github.com/opencontainers/distribution-spec/tree/v1.1.1).
- Additive changes preferred; no public API breaks outside major version.
- Follow spec MUST/MUST NOT. On ambiguity, mirror oras-go unless it conflicts with .NET idioms.
- Go → C# translation:
  - `context.Context` → trailing `CancellationToken` (defaultable).
  - `(T, error)` → `Task<T>` + typed exceptions.
  - Interfaces → `I`-prefixed. `io.Reader`/`Writer` → `Stream`. `[]byte` → `ReadOnlyMemory<byte>`/`Span<byte>`.
  - Functional options → option classes. Async-only I/O; no `.Result`/`.Wait()`.
- Public spec types: XML doc link to relevant spec section.
- Validate digest + size + media type early; `artifactType` non-empty if set.
- JSON property names + omission aligned with spec (conditional ignore attrs).
- HTTP 4xx → domain exceptions; 5xx → surfaced with context. Digest mismatch → hard-fail.

## Feature Checklist
1. Spec section linked in XML docs.
2. Media type constant in `Oci.MediaType` / `Docker.MediaType`.
3. Digest + size validated immediately.
4. JSON shape correct (names, optional/required, omission).
5. HTTP: correct method + status; 4xx mapped, 5xx surfaced.
6. Tests: MUST scenario + negative case.

## Core Concepts
- **Artifact**: any OCI object. Identity = descriptor (media type + digest + size).
- **Referrers**: manifests with `subject` → another digest. Validate subject.
- **References**: accept tags, normalize to digest early. Parse error ≠ not found.
- **Media vs Artifact Type**: media type drives processing; `artifactType` is metadata.

## CAS Rules
- Key = digest. Immutable. Duplicate push = no-op.
- Ingest: stream → hash → compare declared → store or throw.
- Tags are pointers; resolve to digest early. No silent rewrites.

## Coding Style
- File-scoped namespaces: `namespace OrasProject.Oras.<Area>;`
- Explicit access modifiers. Small, single-responsibility types.
- Minimal public API. Internal helpers stay internal.
- Spaces not tabs in all files (`.editorconfig`; CI super-linter enforces).
- `required` for logically mandatory properties.
- No speculative abstractions. No unnecessary dependencies.
- No dead code — remove unreachable branches; verify with coverage.
- Name tuple elements in return types (e.g., `(Descriptor Descriptor, byte[] Content)`).
- Line length: target 100, hard limit 120. URLs and other non-breakable strings may exceed the limit.

## Serialization
- `System.Text.Json` with `[JsonPropertyName]` / conditional ignore.
- Immutable value objects; factory methods for validation.
- **All JSON goes through `OciJsonSerializer`** (`OrasProject.Oras.Serialization`). No direct `JsonSerializer` calls. Ensures Go-compatible encoding (`+` not `\u002B`).
- Serialization types are internal. Domain types have no serialization logic.

## Exceptions & Validation
- Reuse `OrasProject.Oras.Exceptions` types. New type only for distinct category.
- Validate early: digests, media types, sizes, artifactType.

## Async & I/O
- Async-first. No blocking `.Result` / `.Wait()`.
- All async methods **must** use `Async` suffix (e.g., `FetchAsync`, `ResolveAsync`).
- `CancellationToken` always last parameter; use `= default` on public and interface methods only.

## Testing
- Mirror source structure under `tests/OrasProject.Oras.Tests/`.
- Prefer in-memory stores over mocks.
- Cover: happy path, invalid input (null/malformed/truncated), edge case, spec MUST.
- Interface changes: test via interface type for DI verification.
- Parameterize with Theory/InlineData; keep tests focused.
- Raw string literals (`"""`) for JSON fixtures. Wire format > object factories.
- Round-trip: test serialize → deserialize and reverse through same path.
- Internal methods: no default parameters (including `CancellationToken`); prefer overloads.

## Performance
- Stream large payloads; avoid full buffering.
- Minimize allocations (spans/memory).
- Defer expensive computation.

## Security
- Digest mismatch = tamper → throw.
- Validate external inputs; never mutate stored bytes.
- Do not leak internal exception details across boundaries.

## Dependencies & Analyzers
- Zero analyzer warnings (warnings = errors).
- No new deps without justification.
- Do not commit local-only tooling changes (e.g., coverage packages).

## PR Guidance
- One logical change per PR. Summary + rationale + breaking note if any.
- All tests pass. Coverage for new logic. Line lengths verified.

## License Header
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
