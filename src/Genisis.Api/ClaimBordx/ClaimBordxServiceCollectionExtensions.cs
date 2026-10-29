using Genisis.Api.ClaimBordx.Delivery;
using Genisis.Api.ClaimBordx.Revise;

namespace Genisis.Api.ClaimBordx;

/// <summary>Registers the services and repositories of the Claim Bordx screens.</summary>
public static class ClaimBordxServiceCollectionExtensions
{
    /// <summary>Adds a repository behind its interface (a singleton: it holds no state) and the service that uses it for each screen (scoped, one unit of work per request).</summary>
    public static IServiceCollection AddClaimBordxScreens(this IServiceCollection services)
    {
        services.AddSingleton<IBordxDeliveryRepository, BordxDeliveryRepository>();
        services.AddScoped<BordxDeliveryService>();
        services.AddSingleton<IBordxReviseRepository, BordxReviseRepository>();
        services.AddScoped<BordxReviseService>();
        return services;
    }
}
