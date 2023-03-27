using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Exceptions
{
    public class SizeExceedsLimitException : Exception
    {
        public SizeExceedsLimitException()
        {
        }

        public SizeExceedsLimitException(string message)
            : base(message)
        {
        }

        public SizeExceedsLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
