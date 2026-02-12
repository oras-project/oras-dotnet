# Release Checklist

## Overview

This document describes the checklist to publish a release through the [GitHub release page](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository). After releasing, a NuGet package will be published to [nuget.org](https://www.nuget.org/packages/OrasProject.Oras/) for end users.

## Release Process

1. Determine a [SemVer2](https://semver.org/)-valid version prefixed with the letter `v` for release. For example, `v1.0.0-rc.1`.
2. Run the [`release-vote`](.github/workflows/release-vote.yml) workflow from the Actions tab. Provide the tag name and optionally a commit SHA (defaults to latest on `main`). This creates a vote issue listing maintainers and the changelog since the last release (see [example](https://github.com/oras-project/oras-dotnet/issues/103)).
3. After the vote passes, push the tag targeting the voted commit:
   ```bash
   git tag <tag_name> <commit_sha>
   git push origin <tag_name>
   ```
   If the voted commit cannot be found in the _recent commits_, a release branch (e.g. `release-1.0`) is required to be created from the voted commit and used as the target branch.
4. The tag push automatically triggers [`release-github`](.github/workflows/release-github.yml), which creates a **draft** GitHub Release with auto-generated release notes. Pre-release versions (alpha, beta, rc, preview) are automatically marked as pre-releases.
5. Review the draft release on the [Releases page](https://github.com/oras-project/oras-dotnet/releases). Edit the release notes if needed.
6. Publish the release. This triggers [`release-nuget`](.github/workflows/release-nuget.yml), which builds and publishes the NuGet package to [nuget.org](https://www.nuget.org/packages/OrasProject.Oras/) and uploads the `.nupkg` with a SHA256 checksum as release assets.
7. Wait for NuGet to validate the newly released package.
8. Announce the release in the community.

## Retract Process

Due to many reasons (e.g. publish accidentally, security vulnerability discovered), a version can be retracted by the following steps:

1. Determine the version to be retracted.
2. Create an issue to vote for the retraction as well as detailed items to be retracted.
3. After the vote passes, delete or modify the release as well as the corresponding tag depending on the voted items.
4. On `nuget.org`, [unlist](https://learn.microsoft.com/nuget/nuget-org/policies/deleting-packages) and / or [deprecate](https://learn.microsoft.com/nuget/nuget-org/deprecate-packages) the corresponding NuGet package.
5. Announce the retraction in the community.
