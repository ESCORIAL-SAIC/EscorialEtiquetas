using EscorialEtiquetas.Core.Datos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EscorialEtiquetas.Core.Servicios;

public sealed record EstadoHabilitacion(string EtiquetaId, bool Habilitado);

/// <summary>Origen de la habilitación de productos (Calipso, en PostgreSQL).</summary>
public interface IHabilitacionOrigen
{
    Task<IReadOnlyList<EstadoHabilitacion>> ObtenerAsync(string? productoId, CancellationToken ct = default);
}

/// <param name="connectionString">Se evalúa al consultar: si faltan credenciales falla solo la sincronización, no la app.</param>
public sealed class PostgresHabilitacionOrigen(Func<string> connectionString) : IHabilitacionOrigen
{
    private const string Query = """
        select e.productoetiqueta_id as etiqueta_id, u.habilitado_produccion as habilitado
        from producto c
        inner join ud_producto u on c.boextension_id = u.id
        inner join etiquetas_conversioncodigo e on c.id = e.productocalipso_id
        """;

    public async Task<IReadOnlyList<EstadoHabilitacion>> ObtenerAsync(string? productoId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString());
        await conn.OpenAsync(ct);

        // Parametrizado: el original concatenaba el id dentro del SQL.
        await using var cmd = new NpgsqlCommand(
            string.IsNullOrEmpty(productoId) ? Query : Query + " where e.productoetiqueta_id = @id", conn)
        {
            CommandTimeout = 100,
        };
        if (!string.IsNullOrEmpty(productoId))
            cmd.Parameters.AddWithValue("id", productoId);

        var result = new List<EstadoHabilitacion>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = Convert.ToString(reader["etiqueta_id"]) ?? "";
            var habilitado = bool.Parse(Convert.ToString(reader["habilitado"]) ?? "false");
            result.Add(new EstadoHabilitacion(id, habilitado));
        }
        return result;
    }
}

/// <summary>Sincroniza HabilitacionProductos (SQL Server) desde Calipso. Equivale a Functions.ActualizarHabilitados.</summary>
public sealed class HabilitacionService(
    IDbContextFactory<EtiquetasDbContext> dbFactory,
    IHabilitacionOrigen origen,
    TimeProvider reloj)
{
    public async Task<int> ActualizarAsync(string? productoId = null, CancellationToken ct = default)
    {
        var estados = await origen.ObtenerAsync(productoId, ct);
        if (estados.Count == 0)
            return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Una fila por producto: se trae entera en lugar de filtrar con miles de ids
        // (Contains se traduce a OPENJSON, que no existe en SQL Server anteriores a 2016).
        var existentes = (await db.HabilitacionProductos.AsTracking().ToListAsync(ct))
            .GroupBy(h => h.ProductoId)
            .ToDictionary(g => g.Key, g => g.First());

        var ahora = reloj.GetLocalNow().DateTime;
        foreach (var estado in estados)
        {
            if (!existentes.TryGetValue(estado.EtiquetaId, out var h))
            {
                h = new HabilitacionProducto { ProductoId = estado.EtiquetaId, Habilitado = estado.Habilitado, Actualizacion = ahora };
                db.HabilitacionProductos.Add(h);
                existentes[estado.EtiquetaId] = h;
            }
            else if (h.Habilitado != estado.Habilitado)
            {
                h.Habilitado = estado.Habilitado;
                h.Actualizacion = ahora;
            }
        }
        return await db.SaveChangesAsync(ct);
    }
}
