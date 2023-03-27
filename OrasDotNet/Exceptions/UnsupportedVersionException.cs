using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Exceptions
{
<<<<<<< HEAD
    internal class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException() : base("unsupported version") { }
=======
    public class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException()
        {
        }

        public UnsupportedVersionException(string message)
            : base(message)
        {
        }

        public UnsupportedVersionException(string message, Exception inner)
            : base(message, inner)
        {
        }
>>>>>>> interface
    }
}
