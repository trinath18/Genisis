using Genisis.Api.Worksheet.Adjustment;
using Genisis.Api.Worksheet.Calculation;
using Genisis.Api.Worksheet.Engine;
using Genisis.Api.Worksheet.Enquiry;
using Genisis.Api.Worksheet.Lookups;
using Genisis.Api.Worksheet.SendToAccounts;
using Genisis.Api.Worksheet.SendToAr;
using Genisis.Api.Worksheet.Tracking;

namespace Genisis.Api.Worksheet;

/// <summary>Registers the services and repositories of the Worksheet screens.</summary>
public static class WorksheetServiceCollectionExtensions
{
    /// <summary>
    /// Adds a repository behind its interface and the service that uses it for each screen. Services are scoped (one unit of work per
    /// request); repositories hold no state and are singletons. <c>IDbSessions</c> is registered by <c>AddCaseScreens</c>.
    /// </summary>
    public static IServiceCollection AddWorksheetScreens(this IServiceCollection services)
    {
        services.AddSingleton<IWorksheetLookupRepository, WorksheetLookupRepository>();
        services.AddScoped<WorksheetLookupService>();

        services.AddSingleton<IWorksheetEngineRepository, WorksheetEngineRepository>();
        services.AddScoped<WorksheetEngineService>();
        services.AddSingleton<IPlanBenefitRepository, PlanBenefitRepository>();
        services.AddScoped<PlanLimitService>();
        services.AddSingleton<IPlanTermsRepository, PlanTermsRepository>();
        services.AddScoped<CoPaymentService>();
        services.AddSingleton<IBalanceLimitRepository, BalanceLimitRepository>();
        services.AddScoped<BalanceLimitService>();
        services.AddSingleton<IStayDaysRepository, StayDaysRepository>();
        services.AddScoped<WorksheetCalculator>();
        services.AddSingleton<ICalculationRepository, CalculationRepository>();
        services.AddScoped<CalculationService>();

        services.AddSingleton<IAdjustmentRepository, AdjustmentRepository>();
        services.AddScoped<WorksheetAdjustmentService>();
        services.AddSingleton<IDocumentsRepository, DocumentsRepository>();
        services.AddScoped<WorksheetDocumentsService>();
        services.AddScoped<FinalLgService>();

        services.AddSingleton<IEnquiryRepository, EnquiryRepository>();
        services.AddScoped<EnquiryService>();

        services.AddSingleton<ITrackingRepository, TrackingRepository>();
        services.AddScoped<TrackingService>();

        services.AddSingleton<ISendToAccountsRepository, SendToAccountsRepository>();
        services.AddScoped<SendToAccountsService>();

        services.AddSingleton<ISendToArRepository, SendToArRepository>();
        services.AddScoped<SendToArService>();

        return services;
    }
}
