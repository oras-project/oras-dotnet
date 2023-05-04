namespace Oras.Interfaces.Registry
{
    /// <summary>
    /// IManifestStore is a CAS with the ability to stat and delete its content.
    /// Besides, ManifestStore provides reference tagging.
    /// </summary>
    public interface IManifestStore : IBlobStore, IReferencePusher, ITag
    {
    }
}
