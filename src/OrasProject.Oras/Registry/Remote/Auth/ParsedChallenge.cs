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

using System.Collections.Generic;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Represents the result of parsing a registry <c>WWW-Authenticate</c> challenge header.
/// </summary>
/// <param name="Scheme">The parsed authentication <see cref="Challenge.Scheme"/>.</param>
/// <param name="Parameters">
/// The parsed Bearer parameters, or <c>null</c> when the scheme is not
/// <see cref="Challenge.Scheme.Bearer"/> or no parameters are present. A repeated key keeps its last value.
/// </param>
public readonly record struct ParsedChallenge(
    Challenge.Scheme Scheme,
    IReadOnlyDictionary<string, string>? Parameters);
