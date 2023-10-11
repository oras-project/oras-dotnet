# Oras-dotnet

Welcome to the Oras-dotnet library documentation!

## Introduction

Registries are evolving as generic artifact stores. To enable this goal, the ORAS project provides a way to push and pull OCI Artifacts to and from OCI Registries.

Users seeking a generic registry client can benefit from the ORAS CLI, while developers can build their own clients on top of one of the ORAS client libraries.

## What are OCI Registries?

The Open Container Initiative (OCI) defines the specifications and standards for container technologies. This includes the API for working with container registries, known formally as the OCI Distribution Specification (a.k.a. the "distribution-spec").

The distribution-spec was written based on an open-source registry server originally released by the company Docker, which lives on GitHub at [distribution/distribution](https://github.com/distribution/distribution) (now a CNCF project).

There are now a number of other open-source and commercial distribution-spec implementations, a list of which can be found [here](https://oras.land/oci-registries). Registries that implement the distribution-spec are referred to herein as OCI Registries.

## What are OCI Artifacts?

For a long time (pretty much since the beginning), people have been using/abusing OCI Registries to store non-container things. For example, you could upload a video to Docker Hub by just stuffing the video file into a layer in a Docker image (don't do this).

The OCI Artifacts project is an attempt to define an opinionated way to leverage OCI Registries for arbitrary artifacts without masquerading them as container images.

Specifically, OCI Image Manifests have a required field known as `config.mediaType`. According to the guidelines provided by OCI Artifacts, this field provides the ability to differentiate between various types of artifacts.

Artifacts stored in an OCI Registry using this method are referred to herein as OCI Artifacts.

## How ORAS works

ORAS works similarly to tools you may already be familiar with, such as docker. It allows you to push (upload) and pull (download) things to and from an OCI Registry, and also handles login (authentication) and token flow (authorization). What ORAS does differently is shift the focus from container images to other types of artifacts.

ORAS is the de facto tool for working with OCI Artifacts. It treats media types as a critical piece of the puzzle. Container images are never assumed to be the artifact in question.

By default, when pushing artifacts using ORAS, the `config.mediaType` field is set to `application/vnd.unknown.config.v1+json`.

Authors of new OCI Artifacts are thus encouraged to define their own media types specific to their artifact, which their custom client(s) know how to operate on.

If you wish to start publishing OCI Artifacts right away, take a look at the [ORAS Website](https://oras.land).

## API Documentation

For detailed information on how to use the Oras-dotnet library, please refer to the [API Documentation](/oras-dotnet/api).
