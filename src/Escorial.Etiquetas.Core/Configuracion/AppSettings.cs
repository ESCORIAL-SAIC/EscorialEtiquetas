using System.Text.Json.Serialization;

namespace Escorial.Etiquetas.Core.Configuracion;

public enum Zona
{
    Cocinas,
    Termos
}

/// <summary>
/// Reemplazo de Properties.Settings del proyecto WinForms. Se persiste como JSON
/// por usuario (ver <see cref="JsonSettingsStore"/>). Los valores por defecto son
/// los mismos que tenía Settings.settings.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Destino de impresión. Acepta:
    /// <list type="bullet">
    /// <item><c>tcp://10.0.0.50:9100</c> o <c>10.0.0.50:9100</c>: envío directo a la Zebra por red (cualquier SO).</item>
    /// <item>Nombre de cola: en Windows usa el spooler (winspool), en Linux CUPS (<c>lp -o raw</c>).</item>
    /// </list>
    /// </summary>
    public string PrinterName { get; set; } = "Zebra";

    public string ArduinoPort { get; set; } = "COMX";
    public int ArduinoBaudRate { get; set; } = 9600;
    public string MedirDistancia { get; set; } = "a";
    public int DistanciaMaximaCm { get; set; } = 40;

    public string VarFecha { get; set; } = "#FECHA#";
    public string VarSerie { get; set; } = "#NUM#";

    public string UserManual { get; set; } = "";
    public string PasswordManual { get; set; } = "";
    public string UserAuto { get; set; } = "";
    public string PasswordAuto { get; set; } = "";

    public string Caption { get; set; } = "Sistema de Etiquetas - ESCORIAL";

    public string LedAmarilloOn { get; set; } = "b";
    public string LedAmarilloOff { get; set; } = "c";
    public string LedAzulOn { get; set; } = "d";
    public string LedAzulOff { get; set; } = "e";
    public string LedVerdeOn { get; set; } = "f";
    public string LedVerdeOff { get; set; } = "g";
    public string LedRojoOn { get; set; } = "h";
    public string LedRojoOff { get; set; } = "i";

    public bool UsaLogin { get; set; }
    public bool BloqueaProductos { get; set; } = true;
    public string IpPublica { get; set; } = "";

    /// <summary>"COCINAS" o "TERMOS". Vacío equivale a COCINAS.</summary>
    public string Tipo { get; set; } = "";

    public string LineaProduccion { get; set; } = "L1";

    /// <summary>Modo testing: imprime pero no persiste la etiqueta.</summary>
    public bool Testing { get; set; }

    /// <summary>
    /// Imprime una etiqueta de aviso (modelo, gas, equipamiento) cada vez que el operario
    /// cambia la selección. En la app WinForms estaba deshabilitado ("todo: volver a habilitar").
    /// </summary>
    public bool ImprimeAdvertenciaCambioModelo { get; set; }

    /// <summary>
    /// Traducción de rutas de plantillas guardadas en la base (<c>Productos.rutaetiqueta</c>).
    /// Clave: prefijo original (ej. <c>\\servidor\etiquetas</c>), valor: ruta local
    /// (ej. <c>/mnt/etiquetas</c>). Solo hace falta fuera de Windows.
    /// </summary>
    public Dictionary<string, string> MapeoRutas { get; set; } = new();

    [JsonIgnore]
    public Zona Zona => string.Equals(Tipo, "TERMOS", StringComparison.OrdinalIgnoreCase)
        ? Zona.Termos
        : Zona.Cocinas;
}
