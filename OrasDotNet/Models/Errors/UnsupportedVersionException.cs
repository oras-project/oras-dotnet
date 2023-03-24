using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException() : base("unsupported version") { }
    }
}
