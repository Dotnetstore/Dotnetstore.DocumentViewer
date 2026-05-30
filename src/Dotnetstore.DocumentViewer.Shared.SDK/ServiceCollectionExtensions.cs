using Dotnetstore.DocumentViewer.Shared.SDK.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dotnetstore.DocumentViewer.Shared.SDK;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentViewerSdk(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSection = ApiClientOptions.SectionName)
    {
        services
            .AddOptions<ApiClientOptions>()
            .Bind(configuration.GetSection(configurationSection))
            .ValidateOnStart();

        services.AddSingleton<IApiSession, InMemoryApiSession>();
        services.AddTransient<ApiKeyHandler>();
        services.AddTransient<BearerTokenHandler>();

        services.AddHttpClient<IDocumentViewerApiClient, DocumentViewerApiClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseAddress);
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddHttpMessageHandler<BearerTokenHandler>();

        return services;
    }
}
