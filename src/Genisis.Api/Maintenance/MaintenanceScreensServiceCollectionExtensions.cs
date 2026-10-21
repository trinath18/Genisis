using Genisis.Api.Maintenance.AccountTransaction;
using Genisis.Api.Maintenance.MmaEntry;
using Genisis.Api.Maintenance.Security;

namespace Genisis.Api.Maintenance;

/// <summary>Registers the services and repositories of the Maintenance screens (Account Transaction, Security, MMA).</summary>
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
        return services;
    }
}
