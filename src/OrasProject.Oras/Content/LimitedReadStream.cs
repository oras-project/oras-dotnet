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

/// <summary>
/// Represents a read-only stream wrapper that enforces a maximum number of bytes that can be read from the underlying stream.
/// This stream does not support write operations and will throw a <see cref="SizeLimitExceededException"/> if the read limit is exceeded.
/// </summary>
/// <param name="inner">The underlying <see cref="Stream"/> to wrap and limit. Must not be null.</param>
/// <param name="limit">The maximum number of bytes allowed to be read from the stream. Must be non-negative.</param>
/// <remarks>
/// The <see cref="LimitedReadStream"/> provides a safe way to read from streams while enforcing size limits,
/// which is particularly useful for preventing excessive memory usage or enforcing content size constraints
/// in OCI artifact operations. The stream tracks the number of bytes read and will prevent reading beyond
/// the specified limit.
/// 
/// <para>
/// Write operations are explicitly not supported and will throw <see cref="NotSupportedException"/>.
/// The stream properly handles position tracking through both direct position setting and seek operations.
/// </para>
/// </remarks>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is negative.</exception>
/// <exception cref="SizeLimitExceededException">Thrown during read operations when the limit is exceeded.</exception>
internal sealed class LimitedReadStream(Stream inner, long limit) : Stream
{
    private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly long _limit = limit >= 0 ? limit : throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be non-negative.");
    private long _bytesRead = 0;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => Math.Min(_inner.Length, _limit);
    public override long Position
    {
        get => _inner.Position;
        set
        {
            _inner.Position = value;
            // Update bytes read tracking based on new position
            _bytesRead = Math.Min(value, _limit);
        }
    }

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

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write is not supported by LimitedReadStream");

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException("Write is not supported by LimitedReadStream");

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
