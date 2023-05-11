using System;
using System.Net.Http;

namespace Oras.Remote
{
    internal class ErrorUtil
    {
        /// <summary>
        /// ParseErrorResponse parses the error returned by the remote registry.
        /// </summary>
        /// <param name="resp"></param>
        /// <exception cref="Exception"></exception>
        public static void ParseErrorResponse(HttpResponseMessage resp)
        {
            throw new Exception(new
            {
                resp.RequestMessage.Method,
                URL = resp.RequestMessage.RequestUri,
                resp.StatusCode,
                Errors = resp.Content.ReadAsStringAsync().Result
            }.ToString());
        }
    }
}
