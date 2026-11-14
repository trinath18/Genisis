using Genisis.Api.Maintenance.AccountTransaction;
using Genisis.Api.Maintenance.CompanyInfo;
using Genisis.Api.Maintenance.MemberDates;
using Genisis.Api.Maintenance.MmaEntry;
using Genisis.Api.Maintenance.PanelHospitals;
using Genisis.Api.Maintenance.Security;
using Genisis.Api.Maintenance.Suspension;
using Genisis.Api.Maintenance.SurgicalSchedule;

namespace Genisis.Api.Maintenance;

/// <summary>Registers the services and repositories of the Maintenance screens (Account Transaction, Security, MMA, Member, Suspension).</summary>
public static class MaintenanceScreensServiceCollectionExtensions
{
    /// <summary>Adds the Maintenance screens' repositories (stateless, singletons) and services (scoped).</summary>
    public static IServiceCollection AddMaintenanceScreens(this IServiceCollection services)
    {
        services.AddSingleton<IAccountTransactionRepository, AccountTransactionRepository>();
        services.AddScoped<AccountTransactionService>();
        services.AddSingleton<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<DepartmentService>();
        services.AddSingleton<ISecurityUserRepository, SecurityUserRepository>();
        services.AddScoped<SecurityUserService>();
        services.AddSingleton<IMmaEntryRepository, MmaEntryRepository>();
        services.AddScoped<MmaEntryService>();
        services.AddSingleton<IPanelHospitalMaintenanceRepository, PanelHospitalMaintenanceRepository>();
        services.AddScoped<PanelHospitalMaintenanceService>();
        services.AddSingleton<ISurgicalScheduleRepository, SurgicalScheduleRepository>();
        services.AddScoped<SurgicalScheduleService>();
        services.AddSingleton<ICompanyInfoRepository, CompanyInfoRepository>();
        services.AddScoped<CompanyInfoService>();
        services.AddSingleton<IMemberDatesRepository, MemberDatesRepository>();
        services.AddScoped<MemberDatesService>();
        services.AddSingleton<ISuspensionRepository, SuspensionRepository>();
        services.AddScoped<SuspensionService>();
        return services;
    }
}
