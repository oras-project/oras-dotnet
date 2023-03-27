using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class NotFoundException : Exception
    {
        public NotFoundException() : base("not found") { }
=======
    public class NotFoundException : Exception
    {
        public NotFoundException()
        {
        }

        public NotFoundException(string message)
            : base(message)
        {
        }

        public NotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
>>>>>>> interface
    }
}
