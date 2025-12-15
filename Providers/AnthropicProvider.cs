using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Anthropic.SDK;

namespace Vox.Providers;

public class AnthropicProvider : IChatClientProvider
{
    public string Name => "Anthropic";

    public string[] RequiredConfigKeys => new[]
    {
        "ApiKey"
    };

    public bool CanCreate(IConfiguration config)
    {
        return RequiredConfigKeys.All(key => !string.IsNullOrEmpty(config[key]));
    }

    public IChatClient CreateClient(IConfiguration config)
    {
        var apiKey = config["ApiKey"];
        var model = config["Model"] ?? Anthropic.SDK.Constants.AnthropicModels.Claude37Sonnet;

        var anthropicClient = new AnthropicClient(apiKey);

        return new ChatClientBuilder(anthropicClient.Messages)
            .ConfigureOptions(options => options.ModelId = model)
            .Build();
    }
}
