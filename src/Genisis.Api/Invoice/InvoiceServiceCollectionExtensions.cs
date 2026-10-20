namespace Genisis.Api.Invoice;

/// <summary>Registers Invoice Processing.</summary>
public static class InvoiceServiceCollectionExtensions
{
    /// <summary>Adds the invoice repository and service.</summary>
    public static IServiceCollection AddInvoiceScreens(this IServiceCollection services)
    {
        services.AddSingleton<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<InvoiceService>();
        return services;
    }
}
