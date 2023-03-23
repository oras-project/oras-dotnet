using OrasDotNet.Interfaces;
using OrasDotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrasDotnet.Interfaces
{
    internal interface ITarget : IStorage, ITagResolver
    {
          
    }
}