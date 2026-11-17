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

using OrasProject.Oras.Registry.Remote;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class UploadContentStreamTest
{
    private static readonly byte[] _source = [9, 9, 9, 0, 1, 2, 3, 4];

    [Fact]
    public void PositionAndLengthAreRelativeToCallerStartingPosition()
    {
        using var inner = new MemoryStream(_source) { Position = 3 };
        using var stream = new UploadContentStream(inner);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(5, stream.Length);
        Assert.Equal(0, stream.Position);

        stream.Position = 2;
        Assert.Equal(5, inner.Position);
        Assert.Equal(1, stream.Seek(1, SeekOrigin.Begin));
        Assert.Equal(4, inner.Position);
        Assert.Equal(3, stream.Seek(2, SeekOrigin.Current));
        Assert.Equal(4, stream.Seek(-1, SeekOrigin.End));
        Assert.Equal(7, inner.Position);
    }

    [Fact]
    public async Task ReadsResumeFromCallerStartingPositionAfterRewind()
    {
        using var inner = new MemoryStream(_source) { Position = 3 };
        using var stream = new UploadContentStream(inner);
        var buffer = new byte[2];

        Assert.Equal(2, stream.Read(buffer, 0, 2));
        Assert.Equal([0, 1], buffer);
        Assert.Equal(2, stream.Read(buffer.AsSpan()));
        Assert.Equal([2, 3], buffer);

        stream.Position = 0;
        Assert.Equal(2, await stream.ReadAsync(buffer, 0, 2));
        Assert.Equal([0, 1], buffer);
        Assert.Equal(2, await stream.ReadAsync(buffer.AsMemory()));
        Assert.Equal([2, 3], buffer);

        stream.Position = 0;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        Assert.Equal([0, 1, 2, 3, 4], copy.ToArray());
    }

    [Fact]
    public void DisposeLeavesCallerStreamOpen()
    {
        using var inner = new MemoryStream(_source) { Position = 3 };
        var stream = new UploadContentStream(inner);

        stream.Flush();
        stream.Dispose();

        Assert.True(inner.CanRead);
        Assert.Equal(3, inner.Position);
    }

    [Fact]
    public void RejectsWrites()
    {
        using var inner = new MemoryStream(_source);
        using var stream = new UploadContentStream(inner);

        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
    }

    [Fact]
    public void NonSeekableStreamIsReadFromCurrentPosition()
    {
        using var inner = new NonSeekableStream(_source);
        using var stream = new UploadContentStream(inner);
        var buffer = new byte[_source.Length];

        Assert.False(stream.CanSeek);
        Assert.Equal(_source.Length, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(_source, buffer);
    }
}
