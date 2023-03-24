using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace OrasDotnet.Models
{
    internal class MinimumDescriptor
    {
        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MediaType { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
