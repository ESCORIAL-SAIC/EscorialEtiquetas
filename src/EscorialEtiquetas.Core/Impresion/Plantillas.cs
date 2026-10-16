namespace EscorialEtiquetas.Core.Impresion;

/// <summary>Lee la plantilla ZPL apuntada por <c>Productos.rutaetiqueta</c>.</summary>
public interface IPlantillaReader
{
    Task<string> LeerAsync(string ruta, CancellationToken ct = default);
}

public sealed class ArchivoPlantillaReader(Func<IReadOnlyDictionary<string, string>> mapeoRutas) : IPlantillaReader
{
    public Task<string> LeerAsync(string ruta, CancellationToken ct = default) =>
        File.ReadAllTextAsync(TraducirRuta(ruta, mapeoRutas(), OperatingSystem.IsWindows()), ct);

    /// <summary>
    /// Las rutas en la base son de Windows (ej. <c>\\servidor\etiquetas\A.prn</c>).
    /// Fuera de Windows se reemplaza el prefijo según <c>MapeoRutas</c> y se pasan
    /// las barras a <c>/</c>.
    /// </summary>
    public static string TraducirRuta(string ruta, IReadOnlyDictionary<string, string> mapeo, bool esWindows)
    {
        if (esWindows)
            return ruta;

        foreach (var (origen, destino) in mapeo.OrderByDescending(kv => kv.Key.Length))
        {
            if (ruta.StartsWith(origen, StringComparison.OrdinalIgnoreCase))
            {
                ruta = destino.TrimEnd('/') + "/" + ruta[origen.Length..].TrimStart('\\', '/');
                break;
            }
        }
        return ruta.Replace('\\', '/');
    }
}

public static class ZplTemplate
{
    /// <summary>
    /// Reemplaza las variables de la plantilla. La fecha se recorta a "dd/MM/yyyy"
    /// como en Zebra.Print del proyecto original.
    /// </summary>
    public static string Render(string plantilla, string varSerie, int numero, string varFecha, string fecha) =>
        plantilla
            .Replace(varSerie, numero.ToString())
            .Replace(varFecha, fecha.Length >= 10 ? fecha[..10] : fecha);
}

/// <summary>Etiqueta de aviso al cambiar de modelo (ex FrmInicio.ImprimirAdvertenciaCambioModelo).</summary>
public static class AdvertenciaCambioModelo
{
    private static readonly Lazy<string> Logo = new(() =>
    {
        using var stream = typeof(AdvertenciaCambioModelo).Assembly.GetManifestResourceStream("AdvertenciaLogo.zpl")
            ?? throw new InvalidOperationException("Falta el recurso AdvertenciaLogo.zpl.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Mismo ZPL que la versión WinForms: los tres textos y el logo.</summary>
    public static string Generar(string modelo, string gas, string equipamiento) =>
        "^XA " +
        $"   ^FO40,100^A0N,100,100^FD{modelo}^FS " +
        $"   ^FO40,220^A0N,100,100^FD{gas}^FS " +
        $"   ^FO40,340^A0N,100,100^FD{equipamiento}^FS " +
        Logo.Value +
        "^XZ";
}
