namespace Oras.Interfaces
{
    /// <summary>
    /// IBlobTarget is a CAS with the ability to stat and delete its content.
    /// </summary>
    internal interface IBlobTarget : ITarget, IResolver, IDeleter, IReferenceFetcher
    {
    }
}
