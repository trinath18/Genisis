using Genisis.Api.Receipt.Member;
using Genisis.Api.Receipt.Payor;
using Genisis.Api.Receipt.Tracking;

namespace Genisis.Api.Receipt;

/// <summary>Registers the services and repositories of the Receipt screens.</summary>
public static class ReceiptServiceCollectionExtensions
{
    /// <summary>Adds each screen's repository (a singleton: it holds no state) and its service (scoped).</summary>
    public static IServiceCollection AddReceiptScreens(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptTrackingRepository, ReceiptTrackingRepository>();
        services.AddScoped<ReceiptTrackingService>();
        services.AddSingleton<IReceiptMemberRepository, ReceiptMemberRepository>();
        services.AddScoped<ReceiptMemberService>();
        services.AddSingleton<IReceiptPayorRepository, ReceiptPayorRepository>();
        services.AddScoped<ReceiptPayorService>();
        return services;
    }
}
