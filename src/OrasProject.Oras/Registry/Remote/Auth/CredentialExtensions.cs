﻿// Copyright The ORAS Authors.
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

namespace OrasProject.Oras.Registry.Remote.Auth;

public static class CredentialExtensions
{
    
    /// <summary>
    /// IsEmpty determines whether the specified credential object is empty.
    /// </summary>
    /// <param name="credential">The credential object to check.</param>
    /// <returns>
    /// true if all properties of the credential
    /// (Username, Password, RefreshToken, and AccessToken) are null or empty; 
    /// otherwise, false.
    /// </returns>
    public static bool IsEmpty(this Credential credential)
    {
        return string.IsNullOrEmpty(credential.Username) && 
               string.IsNullOrEmpty(credential.Password) && 
               string.IsNullOrEmpty(credential.RefreshToken) && 
               string.IsNullOrEmpty(credential.AccessToken);
    }
}
