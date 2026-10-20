using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Data;

namespace Genisis.Api.Case;

/// <summary>Registers the services and repositories of the nine Case screens added after Registration, Adjustment and Tracking.</summary>
public static class CaseScreensServiceCollectionExtensions
{
    /// <summary>
    /// Adds a repository behind its interface and the service that uses it for each screen. Services are scoped (one unit of work per request);
    /// repositories hold no state and are singletons.
    /// </summary>
    public static IServiceCollection AddCaseScreens(this IServiceCollection services)
    {
        services.AddSingleton<IDbSessions, DbSessions>();

        services.AddSingleton<IPayorRepository, PayorRepository>();
        services.AddScoped<PayorLookupService>();

        services.AddSingleton<IPanelHospitalsRepository, PanelHospitalsRepository>();
        services.AddScoped<PanelHospitalsService>();

        services.AddSingleton<INonDisclosureRepository, NonDisclosureRepository>();
        services.AddScoped<NonDisclosureService>();

        services.AddSingleton<IPayClaimRepository, PayClaimRepository>();
        services.AddScoped<PayClaimService>();

        services.AddSingleton<IMmaRepository, MmaRepository>();
        services.AddScoped<MmaService>();

        services.AddSingleton<IAuditReviewRepository, AuditReviewRepository>();
        services.AddScoped<AuditReviewService>();

        services.AddSingleton<ITransferRepository, TransferRepository>();
        services.AddScoped<TransferService>();

        services.AddSingleton<IListingRepository, ListingRepository>();
        services.AddSingleton<IScannedDocuments, ScannedDocuments>();
        services.AddScoped<ListingService>();

        services.AddSingleton<IAsciiRepository, AsciiRepository>();
        services.AddSingleton<IAsciiFileStore, AsciiFileStore>();
        services.AddScoped<ClaimAsciiService>();
        services.AddScoped<CaseAsciiService>();

        return services;
    }
}
