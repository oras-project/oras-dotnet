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

using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OrasProject.Oras.Tests.Content
{
    public class LimitedStreamTest
    {
        [Fact]
        public void Read_WithinLimit_Succeeds()
        {
            var data = Encoding.UTF8.GetBytes("foobar");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 6);

            var buffer = new byte[6];
            int read = limited.Read(buffer, 0, buffer.Length);

            Assert.Equal(6, read);
            Assert.Equal("foobar", Encoding.UTF8.GetString(buffer));
        }

        [Fact]
        public void Read_ExceedsLimit_Throws()
        {
            var data = Encoding.UTF8.GetBytes("foobar");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 6);

            var buffer = new byte[3];
            int read = limited.Read(buffer, 0, buffer.Length); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("foo", Encoding.UTF8.GetString(buffer));

            read = limited.Read(buffer, 0, buffer.Length); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("bar", Encoding.UTF8.GetString(buffer));

            Assert.Throws<SizeLimitExceededException>(() =>
            {
                limited.ReadExactly(buffer, 0, 1);
            });
        }

        [Fact]
        public async Task ReadAsync_WithinLimit_Succeeds()
        {
            var data = Encoding.UTF8.GetBytes("foobar");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 6);

            var buffer = new byte[6];
            int read = await limited.ReadAsync(buffer);

            Assert.Equal(6, read);
            Assert.Equal("foobar", Encoding.UTF8.GetString(buffer));
        }

        [Fact]
        public async Task ReadAsync_ExceedsLimit_Throws()
        {
            var data = Encoding.UTF8.GetBytes("foobar");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 6);

            var buffer = new byte[3];
            int read = await limited.ReadAsync(buffer); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("foo", Encoding.UTF8.GetString(buffer));

            read = await limited.ReadAsync(buffer); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("bar", Encoding.UTF8.GetString(buffer));

            await Assert.ThrowsAsync<SizeLimitExceededException>(async () =>
            {
                await limited.ReadExactlyAsync(buffer);
            });
        }

        [Fact]
        public void Seek_UpdatesBytesRead()
        {
            var data = Encoding.UTF8.GetBytes("abcdef");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 4);

            limited.Seek(2, SeekOrigin.Begin);
            var buffer = new byte[2];
            int read = limited.Read(buffer, 0, 2);

            Assert.Equal(2, read);
            Assert.Equal("cd", Encoding.UTF8.GetString(buffer));
        }

        [Fact]
        public void SeekAndReadAgain_ValidatesReadBytesCorrectly()
        {
            var data = Encoding.UTF8.GetBytes("hello world");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 11);

            // First read: read 5 bytes
            var buffer = new byte[5];
            int read = limited.Read(buffer, 0, 5);
            Assert.Equal(5, read);
            Assert.Equal("hello", Encoding.UTF8.GetString(buffer));

            // Seek back to beginning
            limited.Seek(0, SeekOrigin.Begin);

            // Second read: can only read 6 more bytes (11 total limit - 5 already read = 6 remaining)
            var buffer2 = new byte[11];
            read = limited.Read(buffer2, 0, 11);
            Assert.Equal(6, read); // Only 6 bytes remaining within the limit
            Assert.Equal("hello ", Encoding.UTF8.GetString(buffer2, 0, read));

            // Verify that attempting to read more throws immediately since limit is reached
            Assert.Throws<SizeLimitExceededException>(() =>
            {
                var buffer3 = new byte[1];
                limited.ReadExactly(buffer3, 0, 1);
            });
        }

        [Fact]
        public async Task Properties_ReflectInnerStreamAndLimit_WhenAccessed()
        {
            var data = Encoding.UTF8.GetBytes("abcdef");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedReadStream(inner, 4);

            Assert.Equal(inner.CanRead, limited.CanRead);
            Assert.Equal(inner.CanSeek, limited.CanSeek);
            Assert.False(limited.CanWrite);
            Assert.Equal(4, limited.Length);
            Assert.Equal(inner.Position, limited.Position);

            Assert.Throws<NotSupportedException>(() =>
            {
                limited.Write(new byte[1]);
            });

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await limited.WriteAsync(new byte[1]);
            });
        }

        [Fact]
        public async Task DisposeAsync_DisposesInnerStream_WhenCalled()
        {
            var data = Encoding.UTF8.GetBytes("test data");
            var inner = new MemoryStream(data);
            var limited = new LimitedReadStream(inner, 10);

            // Verify the inner stream is initially usable
            Assert.True(inner.CanRead);

            // Dispose the limited stream asynchronously
            await limited.DisposeAsync();

            // Verify the inner stream is disposed
            Assert.False(inner.CanRead);
            Assert.False(inner.CanWrite);
            Assert.False(inner.CanSeek);
        }
    }
}
