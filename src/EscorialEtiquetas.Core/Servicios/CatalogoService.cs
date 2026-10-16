using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Datos;
using Microsoft.EntityFrameworkCore;

namespace EscorialEtiquetas.Core.Servicios;

public sealed record Opcion(int Id, string Descripcion)
{
    public override string ToString() => Descripcion;
}

public sealed record OpcionesModelo(IReadOnlyList<Opcion> Gases, IReadOnlyList<Opcion> Equipamientos, byte[]? Imagen);

/// <summary>Modelos, gases, equipamientos e imágenes (TBL_IMAGEN, TBL_GAS, TBL_EQUIP).</summary>
public sealed class CatalogoService(IDbContextFactory<EtiquetasDbContext> dbFactory, AppSettings settings)
{
    public static string Categoria(Zona zona) => zona == Zona.Termos ? "TERMOS" : "COCINAS";

    /// <summary>Modelos de la zona actual, sin traer las imágenes.</summary>
    public async Task<IReadOnlyList<Opcion>> ModelosAsync(CancellationToken ct = default)
    {
        var categoria = Categoria(settings.Zona);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Modelos
            .Where(m => m.Categoria == categoria)
            .Select(m => new Opcion(m.Id, m.Descripcion ?? ""))
            .ToListAsync(ct);
    }

    public async Task<OpcionesModelo?> OpcionesAsync(int modeloId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var modelo = await db.Modelos.FirstOrDefaultAsync(m => m.Id == modeloId, ct);
        if (modelo is null)
            return null;

        var tablaG = modelo.TablaG ?? "";
        var tablaE = modelo.TablaE ?? "";
        var gases = await db.Gases
            .Where(g => g.Identificacion!.Contains(tablaG))
            .Select(g => new Opcion(g.Id, g.Descripcion ?? ""))
            .ToListAsync(ct);
        var equipamientos = await db.Equipamientos
            .Where(e => e.Identificacion!.Contains(tablaE))
            .Select(e => new Opcion(e.Id, e.Descripcion ?? ""))
            .ToListAsync(ct);

        return new OpcionesModelo(gases, equipamientos, modelo.Imagen);
    }

    public async Task GuardarImagenAsync(int modeloId, byte[] imagen, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var modelo = await db.Modelos.AsTracking().FirstOrDefaultAsync(m => m.Id == modeloId, ct)
            ?? throw new InvalidOperationException($"No existe el modelo {modeloId}.");
        modelo.Imagen = imagen;
        await db.SaveChangesAsync(ct);
    }
}
