using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Remote
{
    
    internal static class RegistryUtil
    {
        /// <summary>
        /// BuildScheme returns HTTP scheme used to access the remote registry.
        /// </summary>
        /// <param name="plainHTTP"></param>
        /// <returns></returns>
         public static string BuildScheme(bool plainHTTP)
        {
            if (plainHTTP)
            {
                return "http";
            }

            return "https";
        }
    }
}
