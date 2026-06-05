using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PuertoBB.Infrastructure.Data;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;
using CpIntf = PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using CmIntf = PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

namespace PuertoBB.Infrastructure;

/// <summary>Registro de DbContext + repositorios de cada app en el contenedor DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCamaraPortuariaInfrastructure(this IServiceCollection services, string dbPath)
    {
        // Transient: cada repositorio recibe su propio DbContext de vida corta (app unipersonal,
        // operaciones secuenciales) — evita un contexto compartido con tracking obsoleto entre páginas.
        services.AddDbContext<CamaraPortuariaDbContext>(o => o.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Transient);

        services.AddTransient<CpIntf.IEmpresaRepository, CpRepos.EmpresaRepository>();
        services.AddTransient<CpIntf.IGrupoFacturacionRepository, CpRepos.GrupoFacturacionRepository>();
        services.AddTransient<CpIntf.IReciboRepository, CpRepos.ReciboRepository>();
        services.AddTransient<CpIntf.INotaDeCreditoRepository, CpRepos.NotaDeCreditoRepository>();
        services.AddTransient<CpIntf.IConfiguracionRepository, CpRepos.ConfiguracionRepository>();

        return services;
    }

    public static IServiceCollection AddCentroMaritimoInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<CentroMaritimoDbContext>(o => o.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Transient);

        services.AddTransient<CmIntf.IAgenciaRepository, CmRepos.AgenciaRepository>();
        services.AddTransient<CmIntf.IGrupoFacturacionRepository, CmRepos.GrupoFacturacionRepository>();
        services.AddTransient<CmIntf.IBarcoRepository, CmRepos.BarcoRepository>();
        services.AddTransient<CmIntf.IVoucherRepository, CmRepos.VoucherRepository>();
        services.AddTransient<CmIntf.IContadorVoucherRepository, CmRepos.ContadorVoucherRepository>();
        services.AddTransient<CmIntf.IReciboRepository, CmRepos.ReciboRepository>();
        services.AddTransient<CmIntf.INotaDeCreditoRepository, CmRepos.NotaDeCreditoRepository>();
        services.AddTransient<CmIntf.IConfiguracionRepository, CmRepos.ConfiguracionRepository>();

        return services;
    }
}
