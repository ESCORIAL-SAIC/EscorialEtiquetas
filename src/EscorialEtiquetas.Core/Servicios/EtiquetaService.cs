using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Datos;
using EscorialEtiquetas.Core.Impresion;
using Microsoft.EntityFrameworkCore;

namespace EscorialEtiquetas.Core.Servicios;

public enum ResultadoImpresion
{
    Impresa,
    ProductoNoEncontrado,
    NoHabilitado,
}

public sealed record EtiquetaGenerada(int Numero, string Modelo, string Fecha, string IdProducto, string Usuario);

public sealed record ConteoImpresas(int Hoy, int Usuario, int Ayer);

/// <summary>
/// Lógica que en el proyecto WinForms estaba en Label, Zebra y Functions.Imprimir.
/// </summary>
public sealed class EtiquetaService(
    IDbContextFactory<EtiquetasDbContext> dbFactory,
    AppSettings settings,
    IRawPrinter printer,
    IPlantillaReader plantillas,
    TimeProvider reloj)
{
    public const string FormatoFecha = "dd/MM/yyyy-HH:mm:ss";
    private static readonly string[] LineasValidas = ["L1", "L2", "L3"];

    /// <summary>Antepone la línea de producción ("L2-usuario") al usuario que se guarda en la base.</summary>
    public static string UsuarioConLinea(string baseUser, string? linea)
    {
        if (string.IsNullOrWhiteSpace(baseUser))
            return baseUser;

        var l = (linea ?? "").Trim().ToUpperInvariant();
        if (!LineasValidas.Contains(l))
            return baseUser;

        var prefijo = l + "-";
        return baseUser.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ? baseUser : prefijo + baseUser;
    }

    public static string Visualizacion(string modelo, string gas, string equipamiento) => modelo + gas + equipamiento;

    public async Task<Producto?> BuscarProductoAsync(string modelo, string gas, string equipamiento, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var vis = Visualizacion(modelo, gas, equipamiento);
        return await db.Productos.FirstOrDefaultAsync(p => p.Visualizacion == vis, ct);
    }

    public async Task<int> UltimoSerieAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await UltimoSerieAsync(db, ct);
    }

    private async Task<int> UltimoSerieAsync(EtiquetasDbContext db, CancellationToken ct) =>
        settings.Zona == Zona.Termos
            ? await db.EtiquetasTermotanques.MaxAsync(e => (int?)e.Numero, ct) ?? 0
            : await db.EtiquetasCocinas.MaxAsync(e => (int?)e.Numero, ct) ?? 0;

    public async Task<bool> EstaHabilitadoAsync(string productoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var h = await db.HabilitacionProductos.FirstOrDefaultAsync(x => x.ProductoId == productoId, ct);
        // El original tiraba NullReferenceException si no había registro; acá se trata como no habilitado.
        return h?.Habilitado ?? false;
    }

    /// <summary>Equivalente a Functions.Imprimir.</summary>
    public async Task<ResultadoImpresion> ImprimirAsync(string modelo, string gas, string equipamiento, CancellationToken ct = default)
    {
        var producto = await BuscarProductoAsync(modelo, gas, equipamiento, ct);
        if (producto is null)
            return ResultadoImpresion.ProductoNoEncontrado;

        // TERMOS no usa HabilitacionProductos; "postventa" puede imprimir cualquier producto.
        if (settings.BloqueaProductos
            && settings.Zona != Zona.Termos
            && settings.UserManual != "postventa"
            && !await EstaHabilitadoAsync(producto.Id, ct))
        {
            return ResultadoImpresion.NoHabilitado;
        }

        var etiqueta = await GenerarAsync(producto, gas, equipamiento, guardar: !settings.Testing, ct);

        var plantilla = await plantillas.LeerAsync(producto.RutaEtiqueta
            ?? throw new InvalidOperationException($"El producto {producto.Id} no tiene ruta de etiqueta."), ct);
        var zpl = ZplTemplate.Render(plantilla, settings.VarSerie, etiqueta.Numero, settings.VarFecha, etiqueta.Fecha);
        await printer.EnviarAsync(settings.PrinterName, zpl, ct);

        return ResultadoImpresion.Impresa;
    }

    /// <summary>
    /// Imprime el aviso de cambio de modelo si está habilitado en la configuración.
    /// Devuelve false si la opción está apagada.
    /// </summary>
    public async Task<bool> ImprimirAdvertenciaCambioModeloAsync(string modelo, string gas, string equipamiento, CancellationToken ct = default)
    {
        if (!settings.ImprimeAdvertenciaCambioModelo)
            return false;
        await printer.EnviarAsync(settings.PrinterName, AdvertenciaCambioModelo.Generar(modelo, gas, equipamiento), ct);
        return true;
    }

    private async Task<EtiquetaGenerada> GenerarAsync(Producto producto, string gas, string equipamiento, bool guardar, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var etiqueta = new EtiquetaGenerada(
            Numero: await UltimoSerieAsync(db, ct) + 1,
            Modelo: producto.Descripcion ?? "",
            Fecha: reloj.GetLocalNow().ToString(FormatoFecha, System.Globalization.CultureInfo.InvariantCulture),
            IdProducto: producto.Id,
            Usuario: UsuarioConLinea(settings.UserManual, settings.LineaProduccion));

        if (!guardar)
            return etiqueta;

        if (settings.Zona == Zona.Termos)
        {
            db.EtiquetasTermotanques.Add(new EtiquetaTermotanque
            {
                Numero = etiqueta.Numero,
                Modelo = etiqueta.Modelo,
                Fecha = etiqueta.Fecha,
                IdProducto = etiqueta.IdProducto,
                Usuario = etiqueta.Usuario,
            });
        }
        else
        {
            db.EtiquetasCocinas.Add(new EtiquetaCocina
            {
                Numero = etiqueta.Numero,
                Modelo = etiqueta.Modelo,
                TipoGas = gas,
                Equipo = equipamiento,
                Fecha = etiqueta.Fecha,
                IdProducto = etiqueta.IdProducto,
                Usuario = etiqueta.Usuario,
            });
        }
        await db.SaveChangesAsync(ct);
        return etiqueta;
    }

    /// <summary>Contadores del panel "Etiquetas Impresas" para el producto seleccionado.</summary>
    public async Task<ConteoImpresas> ContarImpresasAsync(string? codigoProducto, CancellationToken ct = default)
    {
        var ahora = reloj.GetLocalNow();
        var hoy = ahora.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var ayer = ahora.AddDays(-1).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var usuario = UsuarioConLinea(settings.UserManual, settings.LineaProduccion);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (settings.Zona == Zona.Termos)
        {
            var q = db.EtiquetasTermotanques.Where(e => e.IdProducto == codigoProducto);
            return new ConteoImpresas(
                await q.CountAsync(e => e.Fecha!.Contains(hoy), ct),
                await q.CountAsync(e => e.Fecha!.Contains(hoy) && e.Usuario == usuario, ct),
                await q.CountAsync(e => e.Fecha!.Contains(ayer), ct));
        }
        else
        {
            var q = db.EtiquetasCocinas.Where(e => e.IdProducto == codigoProducto);
            return new ConteoImpresas(
                await q.CountAsync(e => e.Fecha!.Contains(hoy), ct),
                await q.CountAsync(e => e.Fecha!.Contains(hoy) && e.Usuario == usuario, ct),
                await q.CountAsync(e => e.Fecha!.Contains(ayer), ct));
        }
    }
}
