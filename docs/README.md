# Documentation

This directory contains the documentation for oras-dotnet, built with DocFX.

## Structure

- `docfx.json` - DocFX configuration file
- `index.md` - Main documentation page
- `api/` - API documentation (contains both manual and auto-generated content)
- `toc.yml` - Table of contents

## Building Documentation

To build the documentation locally:

```bash
# Install DocFX globally
dotnet tool install -g docfx

# Navigate to docs directory
cd docs

# Generate API metadata
docfx metadata docfx.json

# Build documentation site
docfx build

# Serve locally (optional)
docfx serve _site
```

## GitHub Actions

### Main Branch Deployment
The documentation is automatically built and deployed to GitHub Pages when changes are pushed to the main branch via the `deploy-to-github-pages.yml` workflow.

### Pull Request Previews
Documentation previews are automatically generated for pull requests that modify documentation files via the `deploy-docfx-preview.yml` workflow. These previews are deployed to unique URLs like:

- Main docs: `https://oras-project.github.io/oras-dotnet/`
- PR preview: `https://oras-project.github.io/oras-dotnet/preview/pr-{number}/`

The preview workflow will:
1. Build DocFX documentation from the PR branch
2. Deploy to a unique preview URL
3. Comment on the PR with the preview link
4. Update the comment on subsequent commits

When a PR is closed, the preview is automatically cleaned up via the `cleanup-docfx-preview.yml` workflow.

## Generated Files

The following files are auto-generated during the DocFX build process and should not be committed:

- `api/*.yml` - API metadata files
- `api/.manifest` - DocFX manifest
- `_site/` - Built documentation site

These files are excluded via `.gitignore`.