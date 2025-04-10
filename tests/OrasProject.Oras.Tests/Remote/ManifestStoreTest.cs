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
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Content.Digest;
using Index = OrasProject.Oras.Oci.Index;
using Xunit;


namespace OrasProject.Oras.Tests.Remote;

public class ManifestStoreTest : IDisposable
{
    private const string _dockerContentDigestHeader = "Docker-Content-Digest";

    public void Dispose()
    {
        ScopeManager.ResetInstance();
    }
    
    [Fact]
    public async Task ManifestStore_PullReferrersIndexListSuccessfully()
    {
        var expectedIndex = RandomIndex();
        var expectedIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndex));
        var expectedIndexDesc = new Descriptor()
        {
            Digest = ComputeSha256(expectedIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = expectedIndexBytes.Length
        };

        HttpResponseMessage MockedHttpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
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
                res.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                res.Headers.Add(_dockerContentDigestHeader, [expectedIndexDesc.Digest]);
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockedHttpHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var (receivedDesc, receivedManifests) = await repo.PullReferrersIndexList(expectedIndexDesc.Digest, cancellationToken);
        Assert.NotNull(receivedDesc);
        Assert.True(AreDescriptorsEqual(expectedIndexDesc, receivedDesc));
        for (var i = 0; i < receivedManifests.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(expectedIndex.Manifests[i], receivedManifests[i]));
        }
    }

    [Fact]
    public async Task ManifestStore_PullReferrersIndexList_ExceedSizeLimit()
    {
        var expectedIndex = RandomIndex();
        var expectedIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndex));
        var expectedIndexDesc = new Descriptor()
        {
            Digest = ComputeSha256(expectedIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = expectedIndexBytes.Length
        };

        HttpResponseMessage MockedHttpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
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
                res.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                res.Headers.Add(_dockerContentDigestHeader, [expectedIndexDesc.Digest]);
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockedHttpHandler),
            PlainHttp = true,
            MaxMetadataBytes = expectedIndexDesc.Size - 1
        });

        var cancellationToken = new CancellationToken();
        var exception = await Assert.ThrowsAsync<SizeLimitExceededException>(async () => await repo.PullReferrersIndexList(expectedIndexDesc.Digest, cancellationToken));
        Assert.Equal($"content size {expectedIndexDesc.Size} exceeds MaxMetadataBytes {repo.Options.MaxMetadataBytes}", exception.Message);
    }

    [Fact]
    public async Task ManifestStore_PullReferrersIndexListNotFound()
    {
        static HttpResponseMessage MockedHttpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockedHttpHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var (receivedDesc, receivedManifests) = await repo.PullReferrersIndexList("test", cancellationToken);
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
            Digest = ComputeSha256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };

        // second push with image config
        var expectedConfigBytes = """config"""u8.ToArray();
        var expectedConfigDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = ComputeSha256(expectedConfigBytes),
            Size = expectedConfigBytes.Length
        };

        byte[]? receivedManifest = null;

        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method == HttpMethod.Put &&
                (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedConfigDesc.Digest}" ||
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
                    res.Headers.Add(_dockerContentDigestHeader, [expectedManifestDesc.Digest]);
                else res.Headers.Add(_dockerContentDigestHeader, [expectedConfigDesc.Digest]);
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockHttpRequestHandlerAsync),
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
    /// ManifestStore_PushAsyncWithSubjectAndReferrerSupported tests PushAsync method for 
    /// pushing manifest with subject when registry supports referrers API
    /// </summary>
    [Fact]
    public async Task ManifestStore_PushAsyncWithSubjectAndReferrerSupported()
    {
        // first push with image manifest
        var (_, expectedManifestBytes) = RandomManifest();
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };

        // second push with index manifest
        var expectedIndexManifest = new Index()
        {
            Subject = RandomDescriptor(),
            Manifests = [RandomDescriptor(), RandomDescriptor()],
            MediaType = MediaType.ImageIndex,
            ArtifactType = MediaType.ImageIndex,
        };
        var expectedIndexManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndexManifest));
        var expectedIndexManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSha256(expectedIndexManifestBytes),
            Size = expectedIndexManifestBytes.Length,
            ArtifactType = MediaType.ImageIndex,
        };
        byte[]? receivedManifest = null;

        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method == HttpMethod.Put &&
                (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}" ||
                    req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedIndexManifestDesc.Digest}"))
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
                    res.Headers.Add(_dockerContentDigestHeader, [expectedManifestDesc.Digest]);
                else res.Headers.Add(_dockerContentDigestHeader, [expectedIndexManifestDesc.Digest]);
                res.StatusCode = HttpStatusCode.Created;
                res.Headers.Add("OCI-Subject", "test");
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockHttpRequestHandlerAsync),
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
            Digest = ComputeSha256(oldIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = oldIndexBytes.Length
        };

        // first push
        var (firstExpectedManifest, firstExpectedManifestBytes) = RandomManifestWithSubject();
        Assert.NotNull(firstExpectedManifest.Subject);
        var firstExpectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(firstExpectedManifestBytes),
            Size = firstExpectedManifestBytes.Length,
            ArtifactType = MediaType.ImageConfig,
        };
        var firstExpectedReferrersList = new List<Descriptor>(oldIndex.Manifests)
        {
            firstExpectedManifestDesc
        };
        var (firstExpectedIndexReferrersDesc, firstExpectedIndexReferrersBytes) = Index.GenerateIndex(firstExpectedReferrersList);

        // second push
        var (_, secondExpectedManifestBytes) = RandomManifestWithSubject(firstExpectedManifest.Subject);
        var secondExpectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(secondExpectedManifestBytes),
            Size = secondExpectedManifestBytes.Length,
            ArtifactType = MediaType.ImageConfig,
        };
        var secondExpectedReferrersList = new List<Descriptor>(firstExpectedReferrersList)
        {
            secondExpectedManifestDesc
        };
        var (secondExpectedIndexReferrersDesc, secondExpectedIndexReferrersBytes) = Index.GenerateIndex(secondExpectedReferrersList);

        byte[]? receivedManifestContent = null;
        byte[]? receivedIndexContent = null;
        var referrersTag = Referrers.BuildReferrersTag(firstExpectedManifest.Subject);
        var oldIndexDeleted = false;
        var firstIndexDeleted = false;
        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                RequestMessage = req
            };

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
                    response.Headers.Add(_dockerContentDigestHeader, [firstExpectedManifestDesc.Digest]);
                else if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{secondExpectedManifestDesc.Digest}")
                    response.Headers.Add(_dockerContentDigestHeader, [secondExpectedManifestDesc.Digest]);
                else if (!oldIndexDeleted) response.Headers.Add(_dockerContentDigestHeader, [firstExpectedIndexReferrersDesc.Digest]);
                else response.Headers.Add(_dockerContentDigestHeader, [secondExpectedIndexReferrersDesc.Digest]);

                response.StatusCode = HttpStatusCode.Created;
                return response;
            }
            else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                if (oldIndexDeleted) response.Content = new ByteArrayContent(firstExpectedIndexReferrersBytes);
                else response.Content = new ByteArrayContent(oldIndexBytes);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                if (oldIndexDeleted) response.Headers.Add(_dockerContentDigestHeader, [firstExpectedIndexReferrersDesc.Digest]);
                else response.Headers.Add(_dockerContentDigestHeader, [oldIndexDesc.Digest]);
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }
            else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{oldIndexDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(oldIndexBytes);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                response.Headers.Add(_dockerContentDigestHeader, [oldIndexDesc.Digest]);
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }
            else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstExpectedIndexReferrersDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(firstExpectedIndexReferrersBytes);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                response.Headers.Add(_dockerContentDigestHeader, [firstExpectedIndexReferrersDesc.Digest]);
                response.StatusCode = HttpStatusCode.OK;
                return response;

            }
            else if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{oldIndexDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, [oldIndexDesc.Digest]);
                response.StatusCode = HttpStatusCode.Accepted;
                oldIndexDeleted = true;
                return response;
            }
            else if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstExpectedIndexReferrersDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, [firstExpectedIndexReferrersDesc.Digest]);
                response.StatusCode = HttpStatusCode.Accepted;
                firstIndexDeleted = true;
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockHttpRequestHandlerAsync),
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
            Manifests = [RandomDescriptor(), RandomDescriptor()],
            MediaType = MediaType.ImageIndex,
            ArtifactType = MediaType.ImageIndex,
        };

        var expectedIndexManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedIndexManifest));
        var expectedIndexManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSha256(expectedIndexManifestBytes),
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

        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                RequestMessage = req
            };

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
                    response.Headers.Add(_dockerContentDigestHeader, [expectedIndexManifestDesc.Digest]);
                }
                else response.Headers.Add(_dockerContentDigestHeader, [expectedIndexReferrersDesc.Digest]);
                response.StatusCode = HttpStatusCode.Created;
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockHttpRequestHandlerAsync),
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
            Manifests =
            [
                new ()
                {
                    MediaType = MediaType.ImageManifest,
                    Digest = ComputeSha256(oldManifestBytes),
                    Size = oldManifestBytes.Length,
                    ArtifactType = MediaType.ImageManifest,
                }
            ],
            MediaType = MediaType.ImageIndex,
        };
        var oldIndexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(oldIndex));
        var oldIndexDesc = new Descriptor()
        {
            Digest = ComputeSha256(oldIndexBytes),
            MediaType = MediaType.ImageIndex,
            Size = oldIndexBytes.Length
        };

        var expectedManifest = oldManifest;
        Assert.NotNull(expectedManifest.Subject);
        var expectedManifestBytes = oldManifestBytes;
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(oldManifestBytes),
            Size = oldManifestBytes.Length,
            ArtifactType = MediaType.ImageManifest,
        };

        byte[]? receivedManifestContent = null;
        var referrersTag = Referrers.BuildReferrersTag(expectedManifest.Subject);

        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                RequestMessage = req
            };

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}")
            {
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buffer = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buffer, 0);
                    receivedManifestContent = buffer;
                }
                response.Headers.Add(_dockerContentDigestHeader, [expectedManifestDesc.Digest]);
                response.StatusCode = HttpStatusCode.Created;
                return response;
            }
            else if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(oldIndexBytes);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                response.Headers.Add(_dockerContentDigestHeader, [oldIndexDesc.Digest]);
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(MockHttpRequestHandlerAsync),
            PlainHttp = true,
        });

        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);

        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.Equal(expectedManifestBytes, receivedManifestContent);
    }

    [Fact]
    public async Task ManifestStore_DeleteWithSubjectWhenReferrersAPISupported()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };
        var manifestDeleted = false;
        HttpResponseMessage httpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                manifestDeleted = true;
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(httpHandler),
            PlainHttp = true,
        });

        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        repo.SetReferrersState(true);
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        await store.DeleteAsync(manifestDesc, cancellationToken);
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
        Assert.True(manifestDeleted);
    }

    [Fact]
    public async Task ManifestStore_DeleteWithoutSubjectWhenReferrersAPIUnknown()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };
        var manifestDeleted = false;
        HttpResponseMessage httpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                manifestDeleted = true;
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(manifestBytes);
                res.Headers.Add(_dockerContentDigestHeader, [manifestDesc.Digest]);
                res.Content.Headers.Add("Content-Type", [MediaType.ImageManifest]);
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(httpHandler),
            PlainHttp = true,
        });
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        await store.DeleteAsync(manifestDesc, cancellationToken);
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        Assert.True(manifestDeleted);
    }

    [Fact]
    public async Task ManifestStore_DeleteWhenTargetSizeExceedsLimit()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(manifestBytes),
            Size = manifestBytes.Length
        };

        static HttpResponseMessage CustomHttpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(CustomHttpHandler),
            PlainHttp = true,
            MaxMetadataBytes = manifestDesc.Size - 1,
        });
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        var exception = await Assert.ThrowsAsync<SizeLimitExceededException>(async () => await store.DeleteAsync(manifestDesc, cancellationToken));
        Assert.Equal($"content size {manifestDesc.Size} exceeds MaxMetadataBytes {repo.Options.MaxMetadataBytes}", exception.Message);
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
    }

    [Fact]
    public async Task ManifestStore_DeleteWithSubjectWhenReferrersAPINotSupported()
    {
        // first delete image manifest
        var (manifestToDelete, manifestToDeleteBytes) = RandomManifestWithSubject();
        Assert.NotNull(manifestToDelete.Subject);
        var manifestToDeleteDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(manifestToDeleteBytes),
            Size = manifestToDeleteBytes.Length
        };

        // then delete image index
        var indexToDelete = RandomIndex();
        indexToDelete.Subject = manifestToDelete.Subject;
        var indexToDeleteBytes = JsonSerializer.SerializeToUtf8Bytes(indexToDelete);
        var indexToDeleteDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSha256(indexToDeleteBytes),
            Size = indexToDeleteBytes.Length
        };

        // original referrers list
        var oldReferrersList = RandomIndex();
        oldReferrersList.Manifests.Add(manifestToDeleteDesc);
        oldReferrersList.Manifests.Add(indexToDeleteDesc);
        var oldReferrersBytes = JsonSerializer.SerializeToUtf8Bytes(oldReferrersList);
        var oldReferrersDesc = new Descriptor()
        {
            Digest = ComputeSha256(oldReferrersBytes),
            MediaType = MediaType.ImageIndex,
            Size = oldReferrersBytes.Length
        };

        // referrers list after deleting the image manifest
        var firstUpdatedReferrersList = new List<Descriptor>(oldReferrersList.Manifests);
        firstUpdatedReferrersList.Remove(manifestToDeleteDesc);
        var (firstUpdatedIndexReferrersDesc, firstUpdatedIndexReferrersBytes) = Index.GenerateIndex(firstUpdatedReferrersList);

        // referrers list after deleting the index manifest
        var secondUpdatedReferrersList = new List<Descriptor>(firstUpdatedReferrersList);
        secondUpdatedReferrersList.Remove(indexToDeleteDesc);
        var (secondUpdatedIndexReferrersDesc, secondUpdatedIndexReferrersBytes) = Index.GenerateIndex(secondUpdatedReferrersList);


        var manifestDeleted = false;
        var oldIndexDeleted = false;
        var firstUpdatedIndexDeleted = false;
        var imageIndexDeleted = false;
        Assert.NotNull(manifestToDelete.Subject);
        var referrersTag = Referrers.BuildReferrersTag(manifestToDelete.Subject);
        byte[]? receivedIndexContent = null;
        async Task<HttpResponseMessage> httpHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get && req.Method != HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buffer = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buffer, 0);
                    receivedIndexContent = buffer;
                }

                if (oldIndexDeleted)
                {
                    response.Headers.Add(_dockerContentDigestHeader, [secondUpdatedIndexReferrersDesc.Digest]);
                }
                else
                {
                    response.Headers.Add(_dockerContentDigestHeader, [firstUpdatedIndexReferrersDesc.Digest]);
                }
                response.StatusCode = HttpStatusCode.Created;
                return response;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{referrersTag}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                if (oldIndexDeleted)
                {
                    response.Content = new ByteArrayContent(firstUpdatedIndexReferrersBytes);
                    response.Headers.Add(_dockerContentDigestHeader, [firstUpdatedIndexReferrersDesc.Digest]);
                }
                else
                {
                    response.Content = new ByteArrayContent(oldReferrersBytes);
                    response.Headers.Add(_dockerContentDigestHeader, [oldReferrersDesc.Digest]);
                }

                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestToDeleteDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(manifestToDeleteBytes);
                response.Headers.Add(_dockerContentDigestHeader, [manifestToDeleteDesc.Digest]);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageManifest]);
                return response;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{indexToDeleteDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(indexToDeleteBytes);
                response.Headers.Add(_dockerContentDigestHeader, [indexToDeleteDesc.Digest]);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                return response;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{oldReferrersDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(oldReferrersBytes);
                response.Headers.Add(_dockerContentDigestHeader, [oldReferrersDesc.Digest]);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                return response;
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstUpdatedIndexReferrersDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                response.Content = new ByteArrayContent(firstUpdatedIndexReferrersBytes);
                response.Headers.Add(_dockerContentDigestHeader, [firstUpdatedIndexReferrersDesc.Digest]);
                response.Content.Headers.Add("Content-Type", [MediaType.ImageIndex]);
                return response;
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{oldReferrersDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, [oldReferrersDesc.Digest]);
                response.StatusCode = HttpStatusCode.Accepted;
                oldIndexDeleted = true;
                return response;
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{firstUpdatedIndexReferrersDesc.Digest}")
            {
                response.Headers.Add(_dockerContentDigestHeader, [firstUpdatedIndexReferrersDesc.Digest]);
                response.StatusCode = HttpStatusCode.Accepted;
                firstUpdatedIndexDeleted = true;
                return response;
            }

            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestToDeleteDesc.Digest}")
            {
                manifestDeleted = true;
                response.StatusCode = HttpStatusCode.Accepted;
                return response;
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{indexToDeleteDesc.Digest}")
            {
                imageIndexDeleted = true;
                response.StatusCode = HttpStatusCode.Accepted;
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(httpHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);

        // first delete the image manifest
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        await store.DeleteAsync(manifestToDeleteDesc, cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.True(manifestDeleted);
        Assert.True(oldIndexDeleted);
        Assert.Equal(firstUpdatedIndexReferrersBytes, receivedIndexContent);

        // then delete the image index
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        await store.DeleteAsync(indexToDeleteDesc, cancellationToken);
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
        Assert.True(imageIndexDeleted);
        Assert.True(firstUpdatedIndexDeleted);
        Assert.Equal(secondUpdatedIndexReferrersBytes, receivedIndexContent);
    }
}
