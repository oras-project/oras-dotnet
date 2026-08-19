# Contributing

Please start with the [ORAS contributing guide](https://oras.land/community/contributing_guide).

Below are specifics for the oras-dotnet project:

## Prerequisites
- .NET 10 SDK is required **to build this repository**. The SDK version is pinned in
  [`global.json`](global.json). An older SDK cannot build a newer target framework, so the
  .NET 8 SDK alone is not sufficient.
- The .NET 8 runtime is additionally required to **run** the `net8.0` test pass, because
  .NET does not roll forward across major versions by default. Installing both the .NET 8
  and .NET 10 SDKs is the simplest way to get a working local setup.
- These are build-time requirements for contributors only. They do **not** raise the bar for
  consumers of the published NuGet package — see [Target Frameworks](#target-frameworks).

## Target Frameworks
The library multi-targets `net8.0` and `net10.0`, so the published package remains fully
consumable from .NET 8 applications. Building a `net10.0` target requires the .NET 10 SDK,
which is why the SDK prerequisite is higher than the lowest supported target framework.

When adding support for a newer .NET release, **add** a target framework rather than
replacing one — replacing it makes the published NuGet package unresolvable (`NU1202`) for
consumers on the older framework. Existing targets are only dropped in a MAJOR release, and
no earlier than that framework's end of support.

## Build
- Run: `dotnet build`

## Tests and Coverage
- Changes in a pull request should include relevant tests.
- Patch test coverage requirement: at least 80%.
- Run tests: `dotnet test` (runs every target framework)
- Run tests for a single target framework: `dotnet test --framework net8.0`

## Linting
- It's recommended to run `dotnet format` before pushing the commit, to avoid linting errors.

## Commit Sign-off
- All commits must be signed off to satisfy the pull request DCO requirement.
- You can do this with: `git commit -s`