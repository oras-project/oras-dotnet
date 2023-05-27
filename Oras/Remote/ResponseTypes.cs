using System.Text.Json.Serialization;

namespace Oras.Remote
{
    internal static class ResponseTypes
    {
        internal struct RepositoryList
        {
            [JsonPropertyName("repositories")]
            public string[] Repositories { get; set; }
        }

        internal struct TagList
        {
            [JsonPropertyName("tags")]
            public string[] Tags { get; set; }
        }
    }
}
