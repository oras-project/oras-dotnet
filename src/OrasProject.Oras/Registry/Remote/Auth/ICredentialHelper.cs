using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

public interface ICredentialHelper
{
    public Task<Credential> Resolve(string hostname, CancellationToken cancellationToken);
}
