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
            using var limited = new LimitedStream(inner, 6);

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
            using var limited = new LimitedStream(inner, 6);

            var buffer = new byte[3];
            int read = limited.Read(buffer, 0, buffer.Length); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("foo", Encoding.UTF8.GetString(buffer));

            read = limited.Read(buffer, 0, buffer.Length); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("bar", Encoding.UTF8.GetString(buffer));

            Assert.Throws<SizeLimitExceededException>(() =>
            {
                int read = limited.Read(buffer, 0, 1);
            });
        }

        [Fact]
        public async Task ReadAsync_WithinLimit_Succeeds()
        {
            var data = Encoding.UTF8.GetBytes("foobar");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedStream(inner, 6);

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
            using var limited = new LimitedStream(inner, 6);

            var buffer = new byte[3];
            int read = await limited.ReadAsync(buffer); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("foo", Encoding.UTF8.GetString(buffer));

            read = await limited.ReadAsync(buffer); // succeeds
            Assert.Equal(3, read);
            Assert.Equal("bar", Encoding.UTF8.GetString(buffer));

            await Assert.ThrowsAsync<SizeLimitExceededException>(async () =>
            {
                int read = await limited.ReadAsync(buffer);
            });
        }

        [Fact]
        public void Write_PassesThrough()
        {
            using var inner = new MemoryStream();
            using var limited = new LimitedStream(inner, 10);

            var buffer = Encoding.UTF8.GetBytes("abc");
            limited.Write(buffer, 0, buffer.Length);

            inner.Position = 0;
            var readBuffer = new byte[3];
            inner.Read(readBuffer, 0, 3);

            Assert.Equal("abc", Encoding.UTF8.GetString(readBuffer));
        }

        [Fact]
        public void Seek_UpdatesBytesRead()
        {
            var data = Encoding.UTF8.GetBytes("abcdef");
            using var inner = new MemoryStream(data);
            using var limited = new LimitedStream(inner, 4);

            limited.Seek(2, SeekOrigin.Begin);
            var buffer = new byte[2];
            int read = limited.Read(buffer, 0, 2);

            Assert.Equal(2, read);
            Assert.Equal("cd", Encoding.UTF8.GetString(buffer));
        }
    }
}