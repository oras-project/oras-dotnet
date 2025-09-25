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
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace OrasProject.Oras.Content;

internal sealed class LimitedStream(Stream inner, long limit) : Stream
{
    private readonly Stream _inner = inner;
    private readonly long _limit = limit;
    private long _bytesRead = 0;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => Math.Min(_inner.Length, _limit);
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = _inner.Seek(offset, origin);
        // Update bytes read tracking based on new position
        _bytesRead = Math.Min(newPosition, _limit);
        return newPosition;
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesRead >= _limit)
        {
            throw new SizeLimitExceededException($"Content size exceeds limit {_limit} bytes");
        }
        // Limit the read count to not exceed the remaining bytes
        var readLimit = (int)Math.Min(count, _limit - _bytesRead);
        var read = _inner.Read(buffer, offset, readLimit);
        _bytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_bytesRead >= _limit)
        {
            throw new SizeLimitExceededException($"Content size exceeds limit {_limit} bytes");
        }
        // Limit the read count to not exceed the remaining bytes
        var readLimit = (int)Math.Min(buffer.Length, _limit - _bytesRead);
        var limitedBuffer = buffer[..readLimit];
        var read = await _inner.ReadAsync(limitedBuffer, cancellationToken).ConfigureAwait(false);
        _bytesRead += read;
        return read;
    }
}
