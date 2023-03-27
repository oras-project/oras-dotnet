using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    public class InvalidDigestException : Exception
    {
        public InvalidDigestException()
        {
        }

        public InvalidDigestException(string message)
            : base(message)
        {
        }

        public InvalidDigestException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
