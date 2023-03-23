using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class InvalidReferenceException : Exception
    {
        public InvalidReferenceException() : base("invalid reference") {}
    }
}
