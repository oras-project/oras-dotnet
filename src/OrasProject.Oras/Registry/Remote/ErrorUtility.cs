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
using System.Net.Http;
using System.Threading.Tasks;

namespace OrasProject.Oras.Remote
{
    internal class ErrorUtility
    {
        /// <summary>
        /// ParseErrorResponse parses the error returned by the remote registry.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        internal static async Task<Exception> ParseErrorResponse(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new Exception(new
            {
                response.RequestMessage.Method,
                URL = response.RequestMessage.RequestUri,
                response.StatusCode,
                Errors = body
            }.ToString());
        }
    }
}
