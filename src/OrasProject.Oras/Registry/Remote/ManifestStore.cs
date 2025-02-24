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

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Registry.Remote;

public class ManifestStore(Repository repository) : IManifestStore
{
    public Repository Repository { get; init; } = repository;

    /// <summary>
    /// Fetches the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReferenceFromDigest(target.Digest);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd(target.MediaType);
        var response = await Repository.Options.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"Digest {target.Digest} not found");
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != target.MediaType)
            {
                throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: mismatch response Content-Type {mediaType}: expect {target.MediaType}");
            }
            var size = response.Content.Headers.ContentLength;
            if (size != null && size != target.Size)
            {
                throw new HttpIOException(HttpRequestError.InvalidResponse, $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: mismatch Content-Length");
            }
            response.VerifyContentDigest(target.Digest);
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fetches the manifest identified by the reference.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd(Repository.ManifestAcceptHeader());
        var response = await Repository.Options.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Descriptor desc;
                    if (response.Content.Headers.ContentLength == null)
                    {
                        desc = await ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        desc = await response.GenerateDescriptorAsync(remoteReference, cancellationToken).ConfigureAwait(false);
                    }
                    return (desc, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                case HttpStatusCode.NotFound:
                    throw new NotFoundException($"{request.Method} {request.RequestUri}: manifest unknown");
                default:
                    throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns true if the described content exists.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        try
        {
            await ResolveAsync(target.Digest, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        => await PushAsync(expected, content, expected.Digest, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Pushes the manifest content with a reference tag.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        await PushWithIndexingAsync(expected, content, remoteReference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PushWithIndexingAsync pushes the given manifest to the repository with indexing support.
    /// If referrer support is not enabled, the function will first push the content, then process and update 
    /// the referrers index before pushing the content again. It handles both image manifests and index manifests.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task PushWithIndexingAsync(Descriptor expected, Stream content, Reference reference,
        CancellationToken cancellationToken = default)
    {
        switch (expected.MediaType) 
        {   
            case MediaType.ImageManifest:
            case MediaType.ImageIndex:
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                { 
                    // Push the manifest straightaway when the registry supports referrers API
                    await DoPushAsync(expected, content, reference, cancellationToken).ConfigureAwait(false);
                    return;
                }
                
                var contentBytes = await content.ReadAllAsync(expected, cancellationToken).ConfigureAwait(false);
                using (var contentDuplicate = new MemoryStream(contentBytes))
                {
                    // Push the manifest when ReferrerState is Unknown or NotSupported
                    await DoPushAsync(expected, contentDuplicate, reference, cancellationToken).ConfigureAwait(false);
                }
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                {
                    // Early exit when the registry supports Referrers API
                    // No need to index referrers list
                    return;
                }

                using (var contentDuplicate = new MemoryStream(contentBytes))
                {
                    // 1. Index the referrers list using referrers tag schema when manifest contains a subject field
                    //    And the ReferrerState is not supported
                    // 2. Or do nothing when the manifest does not contain a subject field when ReferrerState is not supported/unknown
                    await ProcessReferrersAndPushIndex(expected, contentDuplicate, cancellationToken).ConfigureAwait(false);
                }
                break;
            default:
                await DoPushAsync(expected, content, reference, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// ProcessReferrersAndPushIndex processes the referrers for the given descriptor by deserializing its content
    /// (either as an image manifest or image index), extracting relevant metadata
    /// such as the subject, artifact type, and annotations, and then updates the 
    /// referrers index if applicable.
    /// </summary>
    /// <param name="desc"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ProcessReferrersAndPushIndex(Descriptor desc, Stream content, CancellationToken cancellationToken = default)
    {
        Descriptor? subject = null;
        switch (desc.MediaType)
        {
            case MediaType.ImageIndex:
                var indexManifest = JsonSerializer.Deserialize<Index>(content);
                if (indexManifest?.Subject == null)
                {
                    return;
                }
                subject = indexManifest.Subject;
                desc.ArtifactType = indexManifest.ArtifactType;
                desc.Annotations = indexManifest.Annotations;
                break;
            case MediaType.ImageManifest:
                var imageManifest = JsonSerializer.Deserialize<Manifest>(content);
                if (imageManifest?.Subject == null)
                {
                    return;
                }
                subject = imageManifest.Subject;
                desc.ArtifactType = string.IsNullOrEmpty(imageManifest.ArtifactType) ? imageManifest.Config.MediaType : imageManifest.ArtifactType;
                desc.Annotations = imageManifest.Annotations;
                break;
            default:
                return;
        }
        
        // In this case, the manifest contains a subject field and OCI-Subject Header is not set after pushing the manifest to the registry,
        // which indicates that the registry does not support referrers API
        Repository.SetReferrersState(false);
        await UpdateReferrersIndex(subject, new Referrers.ReferrerChange(desc, Referrers.ReferrerOperation.Add), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// UpdateReferrersIndex updates the referrers index for a given subject by applying the specified referrer changes.
    /// If the referrers index is updated, the new index is pushed to the repository. If referrers 
    /// garbage collection is not skipped, the old index is deleted.
    /// References:
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#pushing-manifests-with-subject
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#deleting-manifests
    /// </summary>
    /// <param name="subject"></param>
    /// <param name="referrerChange"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task UpdateReferrersIndex(Descriptor subject,
       Referrers.ReferrerChange referrerChange, CancellationToken cancellationToken = default)
    {
        // 1. pull the original referrers index list using referrers tag schema
        var referrersTag = Referrers.BuildReferrersTag(subject);
        var (oldDesc, oldReferrers) = await PullReferrersIndexList(referrersTag, cancellationToken).ConfigureAwait(false);
        
        // 2. apply the referrer change to referrers list
        var (updatedReferrers, updateRequired) =
            Referrers.ApplyReferrerChanges(oldReferrers,  referrerChange);
        if (!updateRequired)
        {
            return;
        }

        // 3. push the updated referrers list using referrers tag schema
        if (updatedReferrers.Count > 0 || repository.Options.SkipReferrersGc)
        {
            // push a new index in either case:
            // 1. the referrers list has been updated with a non-zero size
            // 2. OR the updated referrers list is empty but referrers GC
            //    is skipped, in this case an empty index should still be pushed
            //    as the old index won't get deleted
            var (indexDesc, indexContent) = Index.GenerateIndex(updatedReferrers);
            using (var content = new MemoryStream(indexContent))
            {
                await DoPushAsync(indexDesc, content, Repository.ParseReference(referrersTag), cancellationToken).ConfigureAwait(false);
            }
        }
        
        if (repository.Options.SkipReferrersGc || Descriptor.IsNullOrInvalid(oldDesc))
        {
            // Skip the delete process if SkipReferrersGc is set to true or the old Descriptor is empty or null
            return;
        }
        
        // 4. delete the dangling original referrers index, if applicable
        await DeleteAsync(oldDesc, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// PullReferrersIndexList retrieves the referrers index list associated with the given referrers tag.
    /// It fetches the index manifest from the repository, deserializes it into an `Index` object, 
    /// and returns the descriptor along with the list of manifests (referrers). If the referrers index is not found, 
    /// an empty descriptor and an empty list are returned.
    /// </summary>
    /// <param name="referrersTag"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<(Descriptor?, IList<Descriptor>)> PullReferrersIndexList(String referrersTag, CancellationToken cancellationToken = default)
    {
        try
        {
            var (desc, content) = await FetchAsync(referrersTag, cancellationToken).ConfigureAwait(false);
            var index = JsonSerializer.Deserialize<Index>(content);
            if (index == null)
            {
                throw new JsonException($"null index manifests list when pulling referrers index list for referrers tag {referrersTag}");
            }
            return (desc, index.Manifests);
        }
        catch (NotFoundException)
        {
            return (null, ImmutableArray<Descriptor>.Empty);
        }
    }
    
    
    /// <summary>
    /// Pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="stream"></param>
    /// <param name="contentReference"></param>
    /// <param name="cancellationToken"></param>
    private async Task DoPushAsync(Descriptor expected, Stream stream, Reference remoteReference, CancellationToken cancellationToken)
    {
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentLength = expected.Size;
        request.Content.Headers.Add("Content-Type", expected.MediaType);
        var client = Repository.Options.HttpClient;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        response.CheckOciSubjectHeader(Repository);
        response.VerifyContentDigest(expected.Digest);
    }

    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        var url = new UriFactory(remoteReference, Repository.Options.PlainHttp).BuildRepositoryManifest();
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        request.Headers.Accept.ParseAdd(Repository.ManifestAcceptHeader());
        using var response = await Repository.Options.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.GenerateDescriptorAsync(remoteReference, cancellationToken).ConfigureAwait(false),
            HttpStatusCode.NotFound => throw new NotFoundException($"Reference {reference} not found"),
            _ => throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Tags a manifest descriptor with a reference string.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        var remoteReference = Repository.ParseReference(reference);
        using var contentStream = await FetchAsync(descriptor, cancellationToken).ConfigureAwait(false);
        await DoPushAsync(descriptor, contentStream, remoteReference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the manifest content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await DeleteWithIndexing(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// DeleteWithIndexing deletes the specified target (Descriptor) from the repository,
    /// handling referrer indexing if necessary.
    /// </summary>
    /// <param name="target">The target descriptor to delete.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed. Defaults to default.</param>
    /// <returns></returns>
    private async Task DeleteWithIndexing(Descriptor target, CancellationToken cancellationToken = default)
    {
        switch (target.MediaType)
        {
            case MediaType.ImageManifest:
            case MediaType.ImageIndex:
                if (Repository.ReferrersState == Referrers.ReferrersState.Supported)
                {
                    // referrers API is available, no client-side indexing needed
                    await Repository.DeleteAsync(target, true, cancellationToken).ConfigureAwait(false);
                    return;
                }

                Repository.LimitSize(target, Repository.Options.MaxMetadataBytes);
                var manifest = await Repository.FetchAllAsync(target, cancellationToken).ConfigureAwait(false);
                using (var manifestStream = new MemoryStream(manifest))
                {
                    await IndexReferrersForDelete(target, manifestStream, cancellationToken).ConfigureAwait(false);
                }
                break;
        }
        await Repository.DeleteAsync(target, true, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// IndexReferrersForDelete indexes referrers for manifests with a subject field on manifest delete.
    /// References:
    ///   - Latest spec: https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md#deleting-manifests
    /// </summary>
    /// <param name="target"></param>
    /// <param name="manifestContent"></param>
    /// <param name="cancellationToken"></param>
    private async Task IndexReferrersForDelete(Descriptor target, Stream manifestContent, CancellationToken cancellationToken = default)
    {
        Descriptor subject;
        switch (target.MediaType)
        {
            case MediaType.ImageManifest:
                var imageManifest = JsonSerializer.Deserialize<Manifest>(manifestContent);
                if (imageManifest?.Subject == null)
                {
                    // no subject, no indexing needed
                    return;
                }
                subject = imageManifest.Subject;
                break;
            case MediaType.ImageIndex:
                var imageIndex = JsonSerializer.Deserialize<Index>(manifestContent);
                if (imageIndex?.Subject == null)
                {
                    // no subject, no indexing needed
                    return;
                }
                subject = imageIndex.Subject;
                break;
            default:
                return;
        }

        var isReferrersSupported = await Repository.PingReferrersAsync(cancellationToken).ConfigureAwait(false);
        if (isReferrersSupported)
        {
            // referrers API is available, no client-side indexing needed
            return;
        }
        await UpdateReferrersIndex(subject, new Referrers.ReferrerChange(target, Referrers.ReferrerOperation.Delete), cancellationToken)
            .ConfigureAwait(false);
    }
}
