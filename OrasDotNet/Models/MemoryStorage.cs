﻿using OrasDotnet.Interfaces;
using OrasDotNet.Models.Errors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotNet.Models
{
    internal class MemoryStorage : IStorage
    {
        public ConcurrentDictionary<Descriptor, byte[]> Content { get; set; } = new ConcurrentDictionary<Descriptor, byte[]>();

        public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = Content.ContainsKey(target);
            return Task.FromResult(contentExist);
        }

        public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        {
            var contentExist = Content.TryGetValue(target, out byte[] content);
            if (!contentExist)
            {
                throw new Exception($"{target.Digest} : {target.MediaType} : {new NotFoundException().Message}");
            }
            return Task.FromResult<Stream>(new MemoryStream(content));
        }

            public Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
        {
         
            var contentExist = Content.TryGetValue(expected, out byte[] content);
            if (!contentExist)
            {
                throw new Exception($"{expected.Digest} : {expected.MediaType} : {new AlreadyExistsException().Message}");
            }

            using (var memoryStream = new MemoryStream())
            {
                contentStream.CopyTo(memoryStream);
                Content.TryAdd(expected, memoryStream.ToArray());
            }
            return Task.CompletedTask;
        }
    }
}