using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Oras.Remote
{
    internal static class ResponseTypes
    {
        internal class RepositoryList
        {
            [JsonPropertyName("repositories")]
            public string[] Repositories { get; set; }
        }

        internal class TagList
        {
            [JsonPropertyName("tags")]
            public string[] Tags { get; set; }
        }
    }
}
