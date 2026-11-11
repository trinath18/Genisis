using Genisis.Api.Authorize.Dcu;

namespace Genisis.Api.Authorize;

/// <summary>Registers the services and repositories of the Authorize Claims screens.</summary>
public static class AuthorizeServiceCollectionExtensions
{
    /// <summary>Adds each screen's repository (a singleton: it holds no state) and its service (scoped).</summary>
    public static IServiceCollection AddAuthorizeScreens(this IServiceCollection services)
    {
        services.AddSingleton<IDcuReportRepository, DcuReportRepository>();
        services.AddScoped<DcuReportService>();
        return services;
    }
}
