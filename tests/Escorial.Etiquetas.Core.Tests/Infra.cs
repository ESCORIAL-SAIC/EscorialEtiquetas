using Escorial.Etiquetas.Core.Datos;
using Escorial.Etiquetas.Core.Impresion;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Escorial.Etiquetas.Core.Tests;

/// <summary>Base SQLite en memoria con el mismo modelo que SQL Server. Vive mientras viva la instancia.</summary>
public sealed class SqliteDb : IDbContextFactory<EtiquetasDbContext>, IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly DbContextOptions<EtiquetasDbContext> _options;

    public SqliteDb()
    {
        _conn.Open();
        _options = new DbContextOptionsBuilder<EtiquetasDbContext>()
            .UseSqlite(_conn)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public EtiquetasDbContext CreateDbContext() => new(_options);

    public void Seed(Action<EtiquetasDbContext> seed)
    {
        using var db = CreateDbContext();
        seed(db);
        db.SaveChanges();
    }

    public void Dispose() => _conn.Dispose();
}

public sealed class FakePrinter : IRawPrinter
{
    public List<(string Destino, string Datos)> Enviados { get; } = [];

    public Task EnviarAsync(string destino, string datos, CancellationToken ct = default)
    {
        Enviados.Add((destino, datos));
        return Task.CompletedTask;
    }
}

public sealed class FakePlantillas(string contenido) : IPlantillaReader
{
    public List<string> Leidas { get; } = [];

    public Task<string> LeerAsync(string ruta, CancellationToken ct = default)
    {
        Leidas.Add(ruta);
        return Task.FromResult(contenido);
    }
}

public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test", now.Offset, "test", "test");
}
