using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class SizeExceedsLimitException : Exception
    {
        public SizeExceedsLimitException() : base("size exceeds limit") { }
    }
}
