# Contributing

Please start with the [ORAS contributing guide](https://oras.land/community/contributing_guide).

Below are specifics for the oras-dotnet project:

## Prerequisites
- .NET 8 SDK is required.

## Build
- Run: `dotnet build`

## Tests and Coverage
- Changes in a pull request should include relevant tests.
- Patch coverage requirement: at least 80%.
- Run tests: `dotnet test`

## Linting
- It's recommended to run `dotnet format` before pushing the commit, to avoid linting errors.

## Commit Sign-off
- All commits must be signed off to satisfy the pull request DCO requirement.
- You can do this with: `git commit -s`