using OrasDotnet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace OrasDotnet.Interfaces
{
    internal interface IStorage : IReadOnlyStorage
    {
        void Push(Descriptor expected, Stream content, CancellationToken cancellationToken = default);
    }
   
}
