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

using System;
using System.Text.Json.Serialization;


namespace OrasProject.Oras.Models
{
    internal class MinimumDescriptor : IEquatable<MinimumDescriptor>
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
