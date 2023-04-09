using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Interfaces
{
    /// <summary>
    /// IReadOnlyTarget represents a read-only Target.
    /// </summary>
    public interface IReadOnlyTarget : IReadOnlyStorage, IResolver
    {
    }
}
