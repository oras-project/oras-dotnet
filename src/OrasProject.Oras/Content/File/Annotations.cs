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

namespace OrasProject.Oras.Content.File;

/// <summary>
/// Annotation keys used by the FileStore.
/// </summary>
public static class FileStoreAnnotations
{
    /// <summary>
    /// AnnotationDigest is the annotation key for the
    /// digest of the uncompressed content.
    /// </summary>
    public const string AnnotationDigest =
        "io.deis.oras.content.digest";

    /// <summary>
    /// AnnotationUnpack is the annotation key for
    /// indication of unpacking.
    /// </summary>
    public const string AnnotationUnpack =
        "io.deis.oras.content.unpack";
}
