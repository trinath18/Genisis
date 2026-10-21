using Genisis.Api.Finance.ElectronicBanking;
using Genisis.Api.Finance.InvoiceReceipt;
using Genisis.Api.Finance.ReceiptEnquiry;
using Genisis.Api.Finance.ReverseReceipt;
using Genisis.Api.Finance.SuspendActive;
using Genisis.Api.Finance.SunPosting;

namespace Genisis.Api.Finance;

/// <summary>Registers the services and repositories of the Finance screens.</summary>
public static class FinanceServiceCollectionExtensions
{
    /// <summary>
    /// Adds a repository behind its interface and the service that uses it for each screen. Services are scoped (one unit of work per
    /// request); repositories hold no state and are singletons. <c>IDbSessions</c> is registered by <c>AddCaseScreens</c>.
    /// </summary>
    public static IServiceCollection AddFinanceScreens(this IServiceCollection services)
    {
        services.AddSingleton<IFinanceClock, SystemFinanceClock>();

        services.AddSingleton<ISunPostingRepository, SunPostingRepository>();
        services.AddScoped<SunPostingService>();

        services.AddSingleton<IReceiptEnquiryRepository, ReceiptEnquiryRepository>();
        services.AddScoped<ReceiptEnquiryService>();

        services.AddSingleton<IInvoiceReceiptRepository, InvoiceReceiptRepository>();
        services.AddScoped<InvoiceReceiptService>();

        services.AddSingleton<IReverseReceiptRepository, ReverseReceiptRepository>();
        services.AddScoped<ReverseReceiptService>();

        services.AddSingleton<ISuspendActiveRepository, SuspendActiveRepository>();
        services.AddScoped<SuspendActiveService>();

        services.AddOptions<BankFileOptions>().BindConfiguration(BankFileOptions.Section);
        services.AddSingleton<IElectronicBankingRepository, ElectronicBankingRepository>();
        services.AddScoped<ElectronicBankingService>();

        return services;
    }
}
