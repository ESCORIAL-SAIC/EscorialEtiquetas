namespace EscorialEtiquetas.Core.Configuracion;

/// <summary>Líneas de producción disponibles según la zona.</summary>
public static class LineasProduccion
{
    private static readonly string[] Cocinas = ["L1", "L2", "L3"];
    private static readonly string[] Termos = ["LAGA", "LAEL", "LAGE", "LACA"];

    public static IReadOnlyList<string> Para(Zona zona) => zona == Zona.Termos ? Termos : Cocinas;

    public static bool EsValida(string linea) => Cocinas.Contains(linea) || Termos.Contains(linea);
}
