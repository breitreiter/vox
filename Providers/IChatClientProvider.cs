using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Vox.Providers;

public interface IChatClientProvider
{
    string Name { get; }
    string[] RequiredConfigKeys { get; }
    IChatClient CreateClient(IConfiguration config);
    bool CanCreate(IConfiguration config);
}
