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

namespace OrasProject.Oras.Tests.Remote;

/// <summary>
/// A stream implementation that wraps another stream and disables seeking.
/// This is used for testing non-seekable stream scenarios.
/// </summary>
internal class NonSeekableStream : Stream
{
    private readonly Stream _innerStream;

    public NonSeekableStream(byte[] data)
    {
        _innerStream = new MemoryStream(data);
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false; // Explicitly make this non-seekable
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => throw new NotSupportedException("This stream does not support seeking");
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        _innerStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("This stream does not support seeking");

    public override void SetLength(long value) => _innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
