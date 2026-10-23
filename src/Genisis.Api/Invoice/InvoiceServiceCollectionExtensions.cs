using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Genisis.Api.Invoice;

/// <summary>Registers Invoice Processing.</summary>
public static class InvoiceServiceCollectionExtensions
{
    /// <summary>Adds the invoice repository and service.</summary>
    public static IServiceCollection AddInvoiceScreens(this IServiceCollection services)
    {
        services.AddSingleton<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<InvoiceService>();
        services.AddSingleton<IInvoiceEntryRepository, InvoiceEntryRepository>();
        services.AddScoped<InvoiceEntryService>();
        services.AddSingleton<IInvoiceOriginalRepository, InvoiceOriginalRepository>();
        services.AddScoped<InvoiceOriginalService>();
        services.AddSingleton<IHospitalAccountingRepository, HospitalAccountingRepository>();
        services.AddScoped<HospitalAccountingService>();
        services.AddSingleton<IMemberAccountingRepository, MemberAccountingRepository>();
        services.AddScoped<MemberAccountingService>();
        services.AddSingleton<IInvoiceTrackingRepository, InvoiceTrackingRepository>();
        services.AddScoped<InvoiceTrackingService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
