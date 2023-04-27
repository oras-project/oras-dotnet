using System;
using System.Text.Json.Serialization;


namespace Oras.Models
{
    public class MinimumDescriptor : IEquatable<MinimumDescriptor>
    {
        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MediaType { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        public bool Equals(MinimumDescriptor other)
        {
            if (other == null) return false;
            return this.MediaType == other.MediaType && this.Digest == other.Digest && this.Size == other.Size;
        }

        public override int GetHashCode()
        {
            return (this.MediaType + this.Digest + this.Size.ToString()).GetHashCode();
        }
    }
}
