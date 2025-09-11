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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;

internal class LimitedStorage(IStorage storage, long limit) : IStorage
{
    private IStorage _storage = storage;
    private long _pushLimit = limit;

    public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return _storage.ExistsAsync(target, cancellationToken);
    }

    public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return _storage.FetchAsync(target, cancellationToken);
    }

    public Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default)
    {
        if (expected.Size > _pushLimit)
        {
            throw new SizeLimitExceededException($"content size {expected.Size} exceeds push size limit {_pushLimit}");
        }
        return _storage.PushAsync(expected, content, cancellationToken);
    }
}
