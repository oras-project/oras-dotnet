using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class NotFoundException : Exception
    {
        public NotFoundException() : base("Not Found") { }
    }
}
