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
    /// Pushes the content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
        => await PushWithIndexingAsync(expected, content, expected.Digest, cancellationToken).ConfigureAwait(false);
    

    /// <summary>
    /// PushReferenceASync pushes the manifest with a reference tag.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream content, string reference, CancellationToken cancellationToken = default)
    {
        var contentReference = Repository.ParseReference(reference).ContentReference!;
        await PushWithIndexingAsync(expected, content, contentReference, cancellationToken).ConfigureAwait(false);
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
    private async Task PushWithIndexingAsync(Descriptor expected, Stream content, string reference,
        CancellationToken cancellationToken = default)
    {
        switch (expected.MediaType) 
        {   
            case MediaType.ImageManifest:
            case MediaType.ImageIndex:
                if (Repository.ReferrersSupportLevel == Referrers.ReferrersSupportLevel.ReferrersSupported)
                { 
                    // Push the manifest straightaway when the registry supports referrers API
                    await InternalPushAsync(expected, content, reference, cancellationToken).ConfigureAwait(false);
                    return;
                }
                
                var contentBytes = await content.ReadAllAsync(expected, cancellationToken);
                using (var contentDuplicate = new MemoryStream(contentBytes))
                {
                    // Push the manifest when ReferrerState is Unknown or NotSupported
                    await InternalPushAsync(expected, contentDuplicate, reference, cancellationToken).ConfigureAwait(false);
                }
                if (Repository.ReferrersSupportLevel == Referrers.ReferrersSupportLevel.ReferrersSupported)
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
                    await ProcessReferrersAndPushIndex(expected, contentDuplicate, cancellationToken);
                }
                break;
            default:
                await InternalPushAsync(expected, content, reference, cancellationToken);
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
                if (indexManifest?.Subject == null) return;
                subject = indexManifest.Subject;
                desc.ArtifactType = indexManifest.ArtifactType;
                desc.Annotations = indexManifest.Annotations;
                break;
            case MediaType.ImageManifest:
                var imageManifest = JsonSerializer.Deserialize<Manifest>(content); 
                if (imageManifest?.Subject == null) return;
                subject = imageManifest.Subject;
                desc.ArtifactType = string.IsNullOrEmpty(imageManifest.ArtifactType) ? imageManifest.Config.MediaType : imageManifest.ArtifactType;
                desc.Annotations = imageManifest.Annotations;
                break;
            default:
                return;
        }
        
        Repository.SetReferrerSupportLevel(Referrers.ReferrersSupportLevel.ReferrersNotSupported);
        await UpdateReferrersIndex(subject, new Referrers.ReferrerChange(desc, Referrers.ReferrerOperation.ReferrerAdd), cancellationToken);
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
        try
        {
            // 1. pull the original referrers index list using referrers tag schema
            var referrersTag = Referrers.BuildReferrersTag(subject);
            var (oldDesc, oldReferrers) = await PullReferrersIndexList(referrersTag, cancellationToken);
            
            // 2. apply the referrer change to referrers list
            var updatedReferrers =
                Referrers.ApplyReferrerChanges(oldReferrers,  referrerChange);

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
                    await InternalPushAsync(indexDesc, content, referrersTag, cancellationToken).ConfigureAwait(false);
                }
            }
            
            if (repository.Options.SkipReferrersGc || Descriptor.IsEmptyOrNull(oldDesc))
            {
                // Skip the delete process if SkipReferrersGc is set to true or the old Descriptor is empty or null
                return;
            }
            
            // 4. delete the dangling original referrers index, if applicable
            await DeleteAsync(oldDesc, cancellationToken).ConfigureAwait(false);
        }
        catch (NoReferrerUpdateException)
        {
            return;
        }
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
    internal async Task<(Descriptor, IList<Descriptor>)> PullReferrersIndexList(string referrersTag, CancellationToken cancellationToken = default)
    {
        try
        {
            var (desc, content) = await FetchAsync(referrersTag, cancellationToken);
            var index = JsonSerializer.Deserialize<Index>(content);
            if (index == null)
            {
                throw new JsonException("null index manifests list");
            }
            return (desc, index.Manifests);
        }
        catch (NotFoundException)
        {
            return (Descriptor.EmptyDescriptor(), new List<Descriptor>());
        }
    }
    
    
    /// <summary>
    /// Pushes the manifest content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="stream"></param>
    /// <param name="contentReference"></param>
    /// <param name="cancellationToken"></param>
    private async Task InternalPushAsync(Descriptor expected, Stream stream, string contentReference, CancellationToken cancellationToken)
    {
        var remoteReference = Repository.ParseReference(contentReference);
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
        response.CheckOCISubjectHeader(Repository);
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
        await InternalPushAsync(descriptor, contentStream, remoteReference.ContentReference!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the manifest content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await Repository.DeleteAsync(target, true, cancellationToken).ConfigureAwait(false);
}
