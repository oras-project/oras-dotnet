using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Exceptions
{
<<<<<<< HEAD
<<<<<<< HEAD
    internal class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException() : base("unsupported version") { }
=======
=======
>>>>>>> interface
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
<<<<<<< HEAD
>>>>>>> interface
=======
>>>>>>> interface
    }
}
