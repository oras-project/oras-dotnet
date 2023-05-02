namespace Oras.Interfaces
{
    /// <summary>
    /// IManifestTarget is a CAS with the ability to stat and delete its content.
    /// Besides, ManifestTarget provides reference tagging.
    /// </summary>
    internal interface IManifestTarget : IBlobTarget, IReferencePusher, ITag
    {
    }
}
