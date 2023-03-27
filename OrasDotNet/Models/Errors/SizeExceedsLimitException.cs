using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class SizeExceedsLimitException : Exception
    {
        public SizeExceedsLimitException() : base("size exceeds limit") { }
=======
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
>>>>>>> interface
    }
}
