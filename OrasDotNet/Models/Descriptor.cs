using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace OrasDotNet.Models
{
    internal class Descriptor
    {
        /*
    type Descriptor struct {
	// MediaType is the media type of the object this schema refers to.
	MediaType string `json:"mediaType,omitempty"`
	// Digest is the digest of the targeted content.
	Digest digest.Digest `json:"digest"`
	// Size specifies the size in bytes of the blob.
	Size int64 `json:"size"`
}
        */
        // omit if empty
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MediaType { get; set; }
        public string Digest { get; set; }
        public long Size { get; set; }
    }
}
