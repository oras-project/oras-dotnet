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
        /// <param name="resp"></param>
        /// <returns></returns>
        public static async Task<Exception> ParseErrorResponse(HttpResponseMessage resp)
        {
            var body = await resp.Content.ReadAsStringAsync();
            return  new Exception( new
            {
                resp.RequestMessage.Method,
                URL = resp.RequestMessage.RequestUri,
                resp.StatusCode,
                Errors = body
            }.ToString());
        }
    }
}
