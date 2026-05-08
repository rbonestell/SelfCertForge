using SelfCertForge.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace SelfCertForge.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSelfCertForgeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICertificateWorkflowService, DotNetCryptoCertificateWorkflowService>();
        services.AddSingleton<ICertificateExportService>(sp =>
            new CertificateExportService(sp.GetRequiredService<ICertificateStore>()));
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        return services;
    }
}
