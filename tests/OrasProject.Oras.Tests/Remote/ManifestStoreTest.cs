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

using System.Net;
using System.Text;
using System.Text.Json;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Content.Digest;
using Index = OrasProject.Oras.Oci.Index;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class ManifestStoreTest
{
    private const string _dockerContentDigestHeader = "Docker-Content-Digest";
    
    [Fact]
    public async Task ManifestStore_PullReferrersIndexListSuccessfully()
    {
        var expectedIndex = RandomIndex();
        var expectedIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndex));
        var expectedIndexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(expectedIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = expectedIndexBytes.Length
        };
        
        var mockedHttpHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedIndexDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(expectedIndexBytes);
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageIndex });
                res.Headers.Add(_dockerContentDigestHeader, new string[] { expectedIndexDesc.Digest });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockedHttpHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        var (receivedDesc, receivedManifests) = await store.PullReferrersIndexList(expectedIndexDesc.Digest, cancellationToken);
        Assert.True(AreDescriptorsEqual(expectedIndexDesc, receivedDesc));
        for (var i = 0; i < receivedManifests.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(expectedIndex.Manifests[i], receivedManifests[i]));
        }
    }
    
    [Fact]
    public async Task ManifestStore_PullReferrersIndexListNotFound()
    {
        var mockedHttpHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockedHttpHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        var (receivedDesc, receivedManifests) = await store.PullReferrersIndexList("test", cancellationToken);
        Assert.Null(receivedDesc);
        Assert.Empty(receivedManifests);
    }
    
    [Fact]
    public async Task ManifestStore_PushAsyncWithoutSubject()
    {
        // first push with image manifest
        var (_, expectedManifestBytes) = RandomManifest();
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };
        
        // second push with image config
        var expectedConfigBytes = """config"""u8.ToArray();
        var expectedConfigDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = ComputeSHA256(expectedConfigBytes),
            Size = expectedConfigBytes.Length
        };
        
        byte[]? receivedManifest = null;
        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Put && (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedConfigDesc.Digest}" || 
                req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}"))
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values))
                {
                    if ((req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}" &&
                         !values.Contains(MediaType.ImageManifest)) ||
                        (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedConfigDesc.Digest}" &&
                         !values.Contains(MediaType.ImageConfig)))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                }
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buf, 0);
                    receivedManifest = buf;
                }
                if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}") 
                    res.Headers.Add(_dockerContentDigestHeader, new string[] { expectedManifestDesc.Digest });
                else res.Headers.Add(_dockerContentDigestHeader, new string[] { expectedConfigDesc.Digest });
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
        Assert.Equal(expectedManifestBytes, receivedManifest);
        
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedConfigDesc, new MemoryStream(expectedConfigBytes), cancellationToken);
        Assert.Equal(expectedConfigBytes, receivedManifest);
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
    }
    
    
    /// <summary>
    /// ManifestStore_PushAsyncWithSubjectAndReferrerSupported tests PushAsync method for pushing manifest with subject when registry supports referrers API
    /// </summary>
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndReferrerSupported()
    {
        // first push with image manifest
        var (_, expectedManifestBytes) = RandomManifestWithSubject();
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };
        
        // second push with index manifest
        var expectedIndexManifest = new Index()
        {
            Subject = RandomDescriptor(),
            Manifests = new List<Descriptor>{ RandomDescriptor(), RandomDescriptor() },
            MediaType = MediaType.ImageIndex,
            ArtifactType = MediaType.ImageIndex,
        };
        var expectedIndexManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndexManifest));
        var expectedIndexManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(expectedIndexManifestBytes),
            Size = expectedIndexManifestBytes.Length,
            ArtifactType = MediaType.ImageIndex,
        };
        byte[]? receivedManifest = null;
        
        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Put && (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}" || 
                                                 req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}" ))
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values))
                {
                    if ((req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}" &&
                         !values.Contains(MediaType.ImageManifest)) ||
                        (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}" &&
                         !values.Contains(MediaType.ImageIndex)))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                }
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buf, 0);
                    receivedManifest = buf;
                }
                if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}") 
                    res.Headers.Add(_dockerContentDigestHeader, new string[] { expectedManifestDesc.Digest });
                else res.Headers.Add(_dockerContentDigestHeader, new string[] { expectedIndexManifestDesc.Digest });
                res.StatusCode = HttpStatusCode.Created;
                res.Headers.Add("OCI-Subject", "test");
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        
        // first push with image manifest
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
        Assert.Equal(expectedManifestBytes, receivedManifest);
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
        
        // second push with index manifest
        await store.PushAsync(expectedIndexManifestDesc, new MemoryStream(expectedIndexManifestBytes), cancellationToken);
        Assert.Equal(expectedIndexManifestBytes, receivedManifest);
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
    }
    
    
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndReferrerNotSupported()
    {
        var oldIndex = RandomIndex();
        var oldIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(oldIndex));
        var oldIndexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(oldIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = oldIndexBytes.Length
        };
        
        // first push
        var (firstExpectedManifest, firstExpectedManifestBytes) = RandomManifestWithSubject();
        var firstExpectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(firstExpectedManifestBytes),
            Size = firstExpectedManifestBytes.Length,
            ArtifactType = MediaType.ImageConfig,
        };
        var firstExpectedReferrersList = new List<Descriptor>(oldIndex.Manifests);
        firstExpectedReferrersList.Add(firstExpectedManifestDesc);
        var (firstExpectedIndexReferrersDesc, firstExpectedIndexReferrersBytes) = Index.GenerateIndex(firstExpectedReferrersList);
        
        // second push
        var (_, secondExpectedManifestBytes) = RandomManifestWithSubject(firstExpectedManifest.Subject);
        var secondExpectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(secondExpectedManifestBytes),
            Size = secondExpectedManifestBytes.Length,
            ArtifactType = MediaType.ImageConfig,
        };
        var secondExpectedReferrersList = new List<Descriptor>(oldIndex.Manifests);
        secondExpectedReferrersList.Add(secondExpectedManifestDesc);
        var (secondExpectedIndexReferrersDesc, secondExpectedIndexReferrersBytes) = Index.GenerateIndex(secondExpectedReferrersList);
        
        byte[]? receivedManifestContent = null;
        byte[]? receivedIndexContent = null;
        var referrersTag = Referrers.BuildReferrersTag(firstExpectedManifest.Subject);
        var oldIndexDeleted = false;
        var firstIndexDeleted = false;
        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = req;
            
            if (req.Method == HttpMethod.Put && (
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstExpectedManifestDesc.Digest}" || 
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{secondExpectedManifestDesc.Digest}" ||
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}"))
            {
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buffer = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buffer, 0);
                    if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{firstExpectedManifestDesc.Digest}" ||
                        req.RequestUri.AbsolutePath == $"/v2/test/manifests/{secondExpectedManifestDesc.Digest}") receivedManifestContent = buffer;
                    else receivedIndexContent = buffer;
                }
    
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{firstExpectedManifestDesc.Digest}")
                    response.Headers.Add(_dockerContentDigestHeader, new[] { firstExpectedManifestDesc.Digest });
                else if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{secondExpectedManifestDesc.Digest}")
                    response.Headers.Add(_dockerContentDigestHeader, new[] { secondExpectedManifestDesc.Digest });
                else if (!oldIndexDeleted) response.Headers.Add(_dockerContentDigestHeader, new[] { firstExpectedIndexReferrersDesc.Digest });
                else response.Headers.Add(_dockerContentDigestHeader, new[] { secondExpectedIndexReferrersDesc.Digest });
                
                response.StatusCode = HttpStatusCode.Created;
                return response;
            } else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {   
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(oldIndexBytes);
                response.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageIndex });
                if (oldIndexDeleted) response.Headers.Add(_dockerContentDigestHeader, new string[] { firstExpectedIndexReferrersDesc.Digest });
                else response.Headers.Add(_dockerContentDigestHeader, new string[] { oldIndexDesc.Digest });
                response.StatusCode = HttpStatusCode.OK;
                return response;
            } else if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{oldIndexDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, new[] { oldIndexDesc.Digest });
                response.StatusCode = HttpStatusCode.Accepted;
                oldIndexDeleted = true;
                return response;
            } else if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstExpectedIndexReferrersDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, new[] { firstExpectedIndexReferrersDesc.Digest });
                response.StatusCode = HttpStatusCode.Accepted;
                firstIndexDeleted = true;
                return response;
            }
    
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
    
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
    
        // First push with referrer tag schema
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(firstExpectedManifestDesc, new MemoryStream(firstExpectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.Equal(firstExpectedManifestBytes, receivedManifestContent);
        Assert.True(oldIndexDeleted);
        Assert.Equal(firstExpectedIndexReferrersBytes, receivedIndexContent);
        
        
        // Second push with referrer tag schema
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        await store.PushAsync(secondExpectedManifestDesc, new MemoryStream(secondExpectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.Equal(secondExpectedManifestBytes, receivedManifestContent);
        Assert.True(firstIndexDeleted);
        Assert.Equal(secondExpectedIndexReferrersBytes, receivedIndexContent);
    }
    
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndReferrerNotSupportedWithoutOldIndex()
    {
        var expectedIndexManifest = new Index()
        {
            Subject = RandomDescriptor(),
            Manifests = new List<Descriptor>{ RandomDescriptor(), RandomDescriptor() },
            MediaType = MediaType.ImageIndex,
            ArtifactType = MediaType.ImageIndex,
        };
        
        var expectedIndexManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndexManifest));
        var expectedIndexManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(expectedIndexManifestBytes),
            Size = expectedIndexManifestBytes.Length,
            ArtifactType = MediaType.ImageIndex,
        };
        var expectedReferrers = new List<Descriptor>
        {
            expectedIndexManifestDesc,
        };

        var (expectedIndexReferrersDesc, expectedIndexReferrersBytes) = Index.GenerateIndex(expectedReferrers);
        
        byte[]? receivedIndexManifestContent = null;
        byte[]? receivedIndexReferrersContent = null;
        var referrersTag = Referrers.BuildReferrersTag(expectedIndexManifest.Subject);

        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = req;
            
            if (req.Method == HttpMethod.Put && (
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}" || 
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}"))
            {
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buffer = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buffer, 0);
                    if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}") receivedIndexManifestContent = buffer;
                    else receivedIndexReferrersContent = buffer;
                }
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}")
                {
                    response.Headers.Add(_dockerContentDigestHeader, new[] { expectedIndexManifestDesc.Digest });
                } else response.Headers.Add(_dockerContentDigestHeader, new[] { expectedIndexReferrersDesc.Digest });
                response.StatusCode = HttpStatusCode.Created;
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
    
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
    
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedIndexManifestDesc, new MemoryStream(expectedIndexManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.Equal(expectedIndexManifestBytes, receivedIndexManifestContent);
        Assert.Equal(expectedIndexReferrersBytes, receivedIndexReferrersContent);
    }
    
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndNoUpdateRequired()
    {
        var (oldManifest, oldManifestBytes) = RandomManifestWithSubject();
        var oldIndex = new Index()
        {
            Manifests = new List<Descriptor>
            {
                new ()
                {
                    MediaType = MediaType.ImageManifest,
                    Digest = ComputeSHA256(oldManifestBytes),
                    Size = oldManifestBytes.Length,
                    ArtifactType = MediaType.ImageManifest,
                }
            },
            MediaType = MediaType.ImageIndex,
        };
        var oldIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(oldIndex));
        var oldIndexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(oldIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = oldIndexBytes.Length
        };

        var expectedManifest = oldManifest;
        var expectedManifestBytes = oldManifestBytes;
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(oldManifestBytes),
            Size = oldManifestBytes.Length,
            ArtifactType = MediaType.ImageManifest,
        };
        
        byte[]? receivedManifestContent = null;
        var referrersTag = Referrers.BuildReferrersTag(expectedManifest.Subject);

        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = req;
            
            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}")
            {
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buffer = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buffer, 0);
                    receivedManifestContent = buffer;
                }
                response.Headers.Add(_dockerContentDigestHeader, new[] { expectedManifestDesc.Digest });
                response.StatusCode = HttpStatusCode.Created;
                return response;
            } else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {   
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(oldIndexBytes);
                response.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageIndex });
                response.Headers.Add(_dockerContentDigestHeader, new string[] { oldIndexDesc.Digest });
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
    
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
    
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.Equal(expectedManifestBytes, receivedManifestContent);
    }
}
