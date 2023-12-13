var reference = "ghcr.io/oras-project/oras:v1.1.0";
var repo = new Oras.Remote.Repository(reference);

// Get the content digest
var desc = await repo.ResolveAsync(reference);
Console.WriteLine("Digest: {0}", desc.Digest);

// Retrive the manifest content
var content = await repo.Manifests().FetchReferenceAsync(reference);
using (var reader = new StreamReader(content.Stream))
{
    var output = await reader.ReadToEndAsync();
    Console.WriteLine(output);
}