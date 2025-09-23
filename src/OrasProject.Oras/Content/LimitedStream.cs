using OrasProject.Oras.Exceptions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace OrasProject.Oras.Content;

internal sealed class LimitedStream(Stream inner, long limit) : Stream
{
    private Stream _inner = inner;
    private long _limit = limit;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
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
        var read = _inner.Read(buffer, offset, count);
        if (read > _limit)
        {
            throw new SizeLimitExceededException($"Content size exceeds limit {_limit} bytes");
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > _limit)
        {
            throw new SizeLimitExceededException($"Content size exceeds limit {_limit} bytes");
        }
        return read;
    }
}