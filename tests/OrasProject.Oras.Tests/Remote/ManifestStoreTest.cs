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
using Xunit.Abstractions;

namespace OrasProject.Oras.Tests.Remote;

public class ManifestStoreTest
{
    private const string _dockerContentDigestHeader = "Docker-Content-Digest";

    private ITestOutputHelper _output;

    public ManifestStoreTest(ITestOutputHelper output)
    {
        _output = output;
    }
    
    /// <summary>
    /// ManifestStore_PushAsyncWithSubjectAndReferrerSupported tests PushAsync method for pushing manifest with subject when registry supports referrers API
    /// </summary>
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndReferrerSupported()
    {
        var (_, manifestBytes) = RandomManifestWithSubject();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifestBytes),
            Size = manifestBytes.Length
        };
        byte[]? receivedManifest = null;
        
        var mockHttpRequestHandler = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buf, 0);
                    receivedManifest = buf;
                }
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
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
        Assert.Equal(Referrers.ReferrerState.ReferrerUnknown, repo.ReferrerState);
        await store.PushAsync(manifestDesc, new MemoryStream(manifestBytes), cancellationToken);
        Assert.Equal(manifestBytes, receivedManifest);
        Assert.Equal(Referrers.ReferrerState.ReferrerSupported, repo.ReferrerState);
    }
    
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
        Assert.True(Descriptor.IsEmptyOrNull(receivedDesc));
        Assert.Empty(receivedManifests);
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
        Assert.Equal(Referrers.ReferrerState.ReferrerUnknown, repo.ReferrerState);
        await store.PushAsync(firstExpectedManifestDesc, new MemoryStream(firstExpectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrerState.ReferrerNotSupported, repo.ReferrerState);
        Assert.Equal(firstExpectedManifestBytes, receivedManifestContent);
        Assert.True(oldIndexDeleted);
        Assert.Equal(firstExpectedIndexReferrersBytes, receivedIndexContent);
        
        
        // Second push with referrer tag schema
        Assert.Equal(Referrers.ReferrerState.ReferrerNotSupported, repo.ReferrerState);
        await store.PushAsync(secondExpectedManifestDesc, new MemoryStream(secondExpectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrerState.ReferrerNotSupported, repo.ReferrerState);
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
    
        Assert.Equal(Referrers.ReferrerState.ReferrerUnknown, repo.ReferrerState);
        await store.PushAsync(expectedIndexManifestDesc, new MemoryStream(expectedIndexManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrerState.ReferrerNotSupported, repo.ReferrerState);
        Assert.Equal(expectedIndexManifestBytes, receivedIndexManifestContent);
        Assert.Equal(expectedIndexReferrersBytes, receivedIndexReferrersContent);
    }
}
