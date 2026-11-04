using Genisis.Api.Payment.ContraAllocation;
using Genisis.Api.Payment.EnquiryHospital;
using Genisis.Api.Payment.EnquiryMember;

namespace Genisis.Api.Payment;

/// <summary>Registers the services and repositories of the Payment screens.</summary>
public static class PaymentServiceCollectionExtensions
{
    /// <summary>Adds each screen's repository (a singleton: it holds no state) and its service (scoped).</summary>
    public static IServiceCollection AddPaymentScreens(this IServiceCollection services)
    {
        services.AddSingleton<IContraAllocationRepository, ContraAllocationRepository>();
        services.AddScoped<ContraAllocationService>();
        services.AddSingleton<IPaymentEnquiryHospitalRepository, PaymentEnquiryHospitalRepository>();
        services.AddScoped<PaymentEnquiryHospitalService>();
        services.AddSingleton<IPaymentEnquiryMemberRepository, PaymentEnquiryMemberRepository>();
        services.AddScoped<PaymentEnquiryMemberService>();
        return services;
    }
}
