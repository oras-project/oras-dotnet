using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class UnsupportedException : Exception
    {
        public UnsupportedException() : base("unsupported") { }
=======
    public class UnsupportedException : Exception
    {
        public UnsupportedException()
        {
        }

        public UnsupportedException(string message)
            : base(message)
        {
        }

        public UnsupportedException(string message, Exception inner)
            : base(message, inner)
        {
        }
>>>>>>> interface
    }
}
