﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class AlreadyExistsException : Exception
    {
        public AlreadyExistsException() : base("already exists") { }
    }
}