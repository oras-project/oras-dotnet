using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Oras.Remote
{
    internal class ErrorUtil
    {
        /// <summary>
        /// ParseErrorResponse parses the error returned by the remote registry.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        internal static async Task<Exception> ParseErrorResponse(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return  new Exception( new
            {
                response.RequestMessage.Method,
                URL = response.RequestMessage.RequestUri,
                response.StatusCode,
                Errors = body
            }.ToString());
        }
    }
}
