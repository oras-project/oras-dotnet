# Release Checklist

## Overview

This document describes the checklist to publish a release through the [GitHub release page](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository). After releasing, a NuGet package will be published to [nuget.org](https://www.nuget.org/packages/OrasProject.Oras/) for end users.

## Release Process

1. Determine a [SemVer2](https://semver.org/)-valid version prefixed with the letter `v` for release. For example, `v1.0.0-rc.1`.
2. Create an issue to vote for a new release (see [example](https://github.com/oras-project/oras-dotnet/issues/103)).
3. After the vote passes, [draft a release](https://github.com/oras-project/oras-dotnet/releases/new) using the determined version as the tag name, targeting the `main` branch. A tag will be automatically created from the `main` branch when you publish the release.
4. Compose and revise the release note and optionally select `Set as a pre-release` depending on the version.
5. Publish the release on GitHub.
6. A [workflow](https://github.com/oras-project/oras-dotnet/actions/workflows/release-nuget.yml) will be triggered automatically by tag creation mentioned in the step 3 for publishing the release to NuGet.
7. Wait for NuGet to validate the newly released package.
8. Announce the release in the community.

## Retract Process

Due to many reasons (e.g. publish accidentally, security vulnerability discovered), a version can be retracted by the following steps:

1. Determine the version to be retracted.
2. Create an issue to vote for the retraction as well as detailed items to be retracted.
3. After the vote passes, delete or modify the release as well as the corresponding tag depending on the voted items.
4. On `nuget.org`, [unlist](https://learn.microsoft.com/nuget/nuget-org/policies/deleting-packages) and / or [deprecate](https://learn.microsoft.com/nuget/nuget-org/deprecate-packages) the corresponding NuGet package.
5. Announce the retraction in the community.
