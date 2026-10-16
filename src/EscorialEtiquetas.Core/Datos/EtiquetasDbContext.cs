using EscorialEtiquetas.Core.Configuracion;
using Microsoft.EntityFrameworkCore;

namespace EscorialEtiquetas.Core.Datos;

public class EtiquetasDbContext(DbContextOptions<EtiquetasDbContext> options) : DbContext(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<EtiquetaCocina> EtiquetasCocinas => Set<EtiquetaCocina>();
    public DbSet<EtiquetaTermotanque> EtiquetasTermotanques => Set<EtiquetaTermotanque>();
    public DbSet<HabilitacionProducto> HabilitacionProductos => Set<HabilitacionProducto>();
    public DbSet<Gas> Gases => Set<Gas>();
    public DbSet<Equipamiento> Equipamientos => Set<Equipamiento>();
    public DbSet<Modelo> Modelos => Set<Modelo>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
}

/// <summary>
/// Crea contextos contra el SQL Server de la zona configurada en ese momento
/// (COCINAS o TERMOS), igual que hacía EtiquetasEntities leyendo Settings.Tipo.
/// </summary>
public sealed class ZonaDbContextFactory(AppSettings settings, LocalSecrets secrets) : IDbContextFactory<EtiquetasDbContext>
{
    public EtiquetasDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EtiquetasDbContext>()
            .UseSqlServer(secrets.SqlConnectionString(settings.Zona))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new EtiquetasDbContext(options);
    }
}
