namespace OrasProject.Oras.Interfaces.Registry
{
    /// <summary>
    /// IManifestStore is a CAS with the ability to stat and delete its content.
    /// Besides, IManifestStore provides reference tagging.
    /// </summary>
    public interface IManifestStore : IBlobStore, IReferencePusher, ITagger
    {
    }
}
