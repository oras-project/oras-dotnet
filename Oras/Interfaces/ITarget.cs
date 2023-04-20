using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Interfaces
{
    /// <summary>
    /// Target is a CAS with generic tags
    /// </summary>
    public interface ITarget : IStorage, ITagResolver
    {
    }
}
