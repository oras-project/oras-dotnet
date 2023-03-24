using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class MissingReferenceException : Exception
    {
        public MissingReferenceException() : base("Missing Reference") { }
    }
}
