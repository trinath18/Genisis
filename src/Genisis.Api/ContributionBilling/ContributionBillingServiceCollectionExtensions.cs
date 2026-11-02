using Genisis.Api.ContributionBilling.RefundListing;

namespace Genisis.Api.ContributionBilling;

/// <summary>Registers the services and repositories of the Contribution Billing screens.</summary>
public static class ContributionBillingServiceCollectionExtensions
{
    /// <summary>Adds each screen's repository (a singleton: it holds no state) and its service (scoped).</summary>
    public static IServiceCollection AddContributionBillingScreens(this IServiceCollection services)
    {
        services.AddSingleton<IRefundListingRepository, RefundListingRepository>();
        services.AddScoped<RefundListingService>();
        return services;
    }
}
