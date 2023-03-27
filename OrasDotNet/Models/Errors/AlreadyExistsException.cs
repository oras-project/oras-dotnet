using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class AlreadyExistsException : Exception
    {
        public AlreadyExistsException() : base("already exists") { }
=======
   public class AlreadyExistsException : Exception
    {
        public AlreadyExistsException()
        {
        }

        public AlreadyExistsException(string message)
            : base(message)
        {
        }

        public AlreadyExistsException(string message, Exception inner)
            : base(message, inner)
        {
        }
>>>>>>> interface
    }
}
