namespace Oras.Interfaces.Registry
{
    /// <summary>
    /// IBlobStore is a CAS with the ability to stat and delete its content.
    /// </summary>
    public interface IBlobStore : IResolver, IDeleter, IReferenceFetcher
    {
    }
}
