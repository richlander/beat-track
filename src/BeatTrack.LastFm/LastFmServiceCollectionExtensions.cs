using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeatTrack.LastFm;

public static class LastFmServiceCollectionExtensions
{
    public static IServiceCollection AddLastFmClient(
        this IServiceCollection services,
        LastFmClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.AddHttpClient<ILastFmClient, LastFmClient>(client => client.BaseAddress = options.EffectiveBaseAddress);

        return services;
    }
}
