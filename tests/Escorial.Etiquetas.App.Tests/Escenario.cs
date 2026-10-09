using Avalonia;
using Avalonia.Headless;
using Escorial.Etiquetas.App.Servicios;
using Escorial.Etiquetas.App.ViewModels;
using Escorial.Etiquetas.Core.Configuracion;
using Escorial.Etiquetas.Core.Datos;
using Escorial.Etiquetas.Core.Servicios;
using Escorial.Etiquetas.Core.Tests;

[assembly: AvaloniaTestApplication(typeof(Escorial.Etiquetas.App.Tests.TestAppBuilder))]

namespace Escorial.Etiquetas.App.Tests;

public static class TestAppBuilder
{
    // Usa la App real (estilos, recursos) con renderizado Skia sin pantalla.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class FakeDialogos : IDialogService
{
    public List<string> Infos { get; } = [];
    public List<string> Errores { get; } = [];
    public bool RespuestaConfirmar { get; set; } = true;
    public string? RespuestaClave { get; set; }
    public int ConfiguracionAbierta { get; private set; }

    public Task InfoAsync(string mensaje) { Infos.Add(mensaje); return Task.CompletedTask; }
    public Task ErrorAsync(string mensaje) { Errores.Add(mensaje); return Task.CompletedTask; }
    public Task<bool> ConfirmarAsync(string mensaje) => Task.FromResult(RespuestaConfirmar);
    public Task<string?> PedirClaveAsync(string mensaje) => Task.FromResult(RespuestaClave);
    public Task<string?> ElegirImagenAsync() => Task.FromResult<string?>(null);
    public Task AbrirConfiguracionAsync() { ConfiguracionAbierta++; return Task.CompletedTask; }
    public Task AbrirImagenesAsync() => Task.CompletedTask;
}

public sealed class FakeSettingsStore : ISettingsStore
{
    public int Guardados { get; private set; }
    public AppSettings Load() => new();
    public void Save(AppSettings settings) => Guardados++;
}

/// <summary>Datos de prueba y servicios reales sobre SQLite en memoria.</summary>
public sealed class Escenario : IDisposable
{
    public SqliteDb Db { get; } = new();
    public FakePrinter Printer { get; } = new();
    public FakeDialogos Dialogos { get; } = new();
    public FakeSettingsStore Store { get; } = new();
    public AppSettings Settings { get; } = new() { UserManual = "jperez", PasswordManual = "1234", PrinterName = "tcp://zebra:9100" };
    public LocalSecrets Secrets { get; } = new(null, null, null, null, "data source=sql-cocinas;user id=x", "data source=sql-termos;user id=x");

    public Escenario()
    {
        Db.Seed(db =>
        {
            db.Usuarios.Add(new Usuario { Id = 1, Nombre = "jperez", NombreCompleto = "J", Contrasena = "1234" });
            db.Usuarios.Add(new Usuario { Id = 2, Nombre = "administrador", NombreCompleto = "A", Contrasena = "admin" });
            db.Modelos.Add(new Modelo { Id = 1, Descripcion = "C56", Categoria = "COCINAS", TablaG = "G1", TablaE = "E1" });
            db.Modelos.Add(new Modelo { Id = 2, Descripcion = "T80", Categoria = "TERMOS", TablaG = "G1", TablaE = "E1" });
            db.Gases.Add(new Gas { Id = 1, Descripcion = "GN", Identificacion = "G1" });
            db.Equipamientos.Add(new Equipamiento { Id = 1, Descripcion = "BLANCA", Identificacion = "E1" });
            db.Productos.Add(new Producto { Id = "P1", Descripcion = "Cocina 56", Visualizacion = "C56GNBLANCA", RutaEtiqueta = "p1.prn" });
            db.Productos.Add(new Producto { Id = "T1", Descripcion = "Termo 80", Visualizacion = "T80GNBLANCA", RutaEtiqueta = "t1.prn" });
            db.HabilitacionProductos.Add(new HabilitacionProducto { ProductoId = "P1", Habilitado = true, Actualizacion = DateTime.Now });
            db.EtiquetasCocinas.Add(new EtiquetaCocina { Numero = 10, Fecha = DateTime.Now.ToString("dd/MM/yyyy-HH:mm:ss"), IdProducto = "P1", Usuario = "L1-jperez" });
        });
    }

    public EtiquetaService Etiquetas => new(Db, Settings, Printer, new FakePlantillas("^XA^FD#NUM#^FS^XZ"), TimeProvider.System);
    public UsuarioService Usuarios => new(Db);
    public CatalogoService Catalogo => new(Db, Settings);

    private sealed class SinOrigen : IHabilitacionOrigen
    {
        public Task<IReadOnlyList<EstadoHabilitacion>> ObtenerAsync(string? productoId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EstadoHabilitacion>>([]);
    }

    public MainViewModel Main() =>
        new(Settings, Etiquetas, Catalogo, Usuarios, new HabilitacionService(Db, new SinOrigen(), TimeProvider.System), Dialogos);

    public ConfigViewModel Config() =>
        new(Settings, Store, Secrets, Usuarios, Dialogos, () => Task.FromResult<IReadOnlyList<string>>(["Zebra_L1", "Zebra_L2"]));

    public void Dispose() => Db.Dispose();
}
