using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class UnsupportedException : Exception
    {
        public UnsupportedException() : base("unsupported") { }
    }
}
