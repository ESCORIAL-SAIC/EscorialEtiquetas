using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Datos;
using EscorialEtiquetas.Core.Impresion;
using EscorialEtiquetas.Core.Servicios;

namespace EscorialEtiquetas.Core.Tests;

public sealed class EtiquetaServiceTests : IDisposable
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 14, 30, 5, TimeSpan.FromHours(-3));
    private const string Plantilla = "^XA^FD#NUM#^FS^FD#FECHA#^FS^XZ";

    private readonly SqliteDb _db = new();
    private readonly FakePrinter _printer = new();
    private readonly FakePlantillas _plantillas = new(Plantilla);
    private readonly AppSettings _settings = new()
    {
        PrinterName = "tcp://10.0.0.50:9100",
        UserManual = "jperez",
        LineaProduccion = "L2",
        Tipo = "COCINAS",
    };

    public EtiquetaServiceTests()
    {
        _db.Seed(db =>
        {
            db.Productos.Add(new Producto { Id = "P1", Descripcion = "Cocina 56", Visualizacion = "C56GNBLANCA", RutaEtiqueta = @"\\srv\etq\p1.prn" });
            db.EtiquetasCocinas.Add(new EtiquetaCocina { Numero = 41, Fecha = "08/10/2026-10:00:00", IdProducto = "P1", Usuario = "L2-jperez" });
            db.EtiquetasTermotanques.Add(new EtiquetaTermotanque { Numero = 900, Fecha = "09/10/2026-09:00:00", IdProducto = "P1" });
        });
    }

    public void Dispose() => _db.Dispose();

    private EtiquetaService Service() => new(_db, _settings, _printer, _plantillas, new FixedTime(Ahora));

    private void Habilitar(string id, bool habilitado) =>
        _db.Seed(db => db.HabilitacionProductos.Add(new HabilitacionProducto { ProductoId = id, Habilitado = habilitado, Actualizacion = DateTime.Now }));

    [Theory]
    [InlineData("jperez", "L1", "L1-jperez")]
    [InlineData("jperez", " l3 ", "L3-jperez")]
    [InlineData("L2-jperez", "L2", "L2-jperez")]
    [InlineData("jperez", "L9", "jperez")]
    [InlineData("jperez", "LAGA", "LAGA-jperez")]
    [InlineData("jperez", "LAXX", "jperez")]
    [InlineData("jperez", null, "jperez")]
    [InlineData("", "L1", "")]
    public void UsuarioConLinea(string usuario, string? linea, string esperado) =>
        Assert.Equal(esperado, EtiquetaService.UsuarioConLinea(usuario, linea));

    [Fact]
    public async Task Imprime_y_guarda_cocina_habilitada()
    {
        Habilitar("P1", true);

        var r = await Service().ImprimirAsync("C56", "GN", "BLANCA");

        Assert.Equal(ResultadoImpresion.Impresa, r);
        var (destino, zpl) = Assert.Single(_printer.Enviados);
        Assert.Equal("tcp://10.0.0.50:9100", destino);
        Assert.Equal("^XA^FD42^FS^FD09/10/2026^FS^XZ", zpl);
        Assert.Equal(@"\\srv\etq\p1.prn", Assert.Single(_plantillas.Leidas));

        await using var db = _db.CreateDbContext();
        var guardada = db.EtiquetasCocinas.Single(e => e.Numero == 42);
        Assert.Equal("Cocina 56", guardada.Modelo);
        Assert.Equal("GN", guardada.TipoGas);
        Assert.Equal("BLANCA", guardada.Equipo);
        Assert.Equal("09/10/2026-14:30:05", guardada.Fecha);
        Assert.Equal("L2-jperez", guardada.Usuario);
    }

    [Fact]
    public async Task Modo_testing_imprime_sin_guardar()
    {
        Habilitar("P1", true);
        _settings.Testing = true;

        var r = await Service().ImprimirAsync("C56", "GN", "BLANCA");

        Assert.Equal(ResultadoImpresion.Impresa, r);
        Assert.Contains("^FD42^FS", Assert.Single(_printer.Enviados).Datos);
        await using var db = _db.CreateDbContext();
        Assert.Equal(1, db.EtiquetasCocinas.Count());
    }

    [Fact]
    public async Task Producto_no_habilitado_no_imprime()
    {
        Habilitar("P1", false);

        var r = await Service().ImprimirAsync("C56", "GN", "BLANCA");

        Assert.Equal(ResultadoImpresion.NoHabilitado, r);
        Assert.Empty(_printer.Enviados);
    }

    [Fact]
    public async Task Sin_registro_de_habilitacion_se_considera_no_habilitado()
    {
        var r = await Service().ImprimirAsync("C56", "GN", "BLANCA");
        Assert.Equal(ResultadoImpresion.NoHabilitado, r);
    }

    [Fact]
    public async Task Postventa_imprime_aunque_no_este_habilitado()
    {
        Habilitar("P1", false);
        _settings.UserManual = "postventa";

        Assert.Equal(ResultadoImpresion.Impresa, await Service().ImprimirAsync("C56", "GN", "BLANCA"));
    }

    [Fact]
    public async Task Sin_bloqueo_de_productos_no_valida_habilitacion()
    {
        Habilitar("P1", false);
        _settings.BloqueaProductos = false;

        Assert.Equal(ResultadoImpresion.Impresa, await Service().ImprimirAsync("C56", "GN", "BLANCA"));
    }

    [Fact]
    public async Task Termos_no_valida_habilitacion_y_guarda_en_su_tabla()
    {
        _settings.Tipo = "termos";

        var r = await Service().ImprimirAsync("C56", "GN", "BLANCA");

        Assert.Equal(ResultadoImpresion.Impresa, r);
        await using var db = _db.CreateDbContext();
        Assert.Contains(db.EtiquetasTermotanques, e => e.Numero == 901 && e.Usuario == "L2-jperez");
        Assert.Equal(1, db.EtiquetasCocinas.Count());
    }

    [Fact]
    public async Task Producto_inexistente()
    {
        Assert.Equal(ResultadoImpresion.ProductoNoEncontrado, await Service().ImprimirAsync("X", "Y", "Z"));
        Assert.Empty(_printer.Enviados);
    }

    [Fact]
    public async Task Ultimo_serie_por_zona()
    {
        Assert.Equal(41, await Service().UltimoSerieAsync());
        _settings.Tipo = "TERMOS";
        Assert.Equal(900, await Service().UltimoSerieAsync());
    }

    [Fact]
    public async Task Ultimo_serie_con_tabla_vacia_es_cero()
    {
        using var vacia = new SqliteDb();
        var svc = new EtiquetaService(vacia, _settings, _printer, _plantillas, TimeProvider.System);
        Assert.Equal(0, await svc.UltimoSerieAsync());
    }

    [Fact]
    public async Task Advertencia_apagada_por_defecto_no_imprime()
    {
        Assert.False(new AppSettings().ImprimeAdvertenciaCambioModelo);
        Assert.False(await Service().ImprimirAdvertenciaCambioModeloAsync("C56", "GN", "BLANCA"));
        Assert.Empty(_printer.Enviados);
    }

    [Fact]
    public async Task Advertencia_encendida_imprime_en_la_impresora_configurada()
    {
        _settings.ImprimeAdvertenciaCambioModelo = true;

        Assert.True(await Service().ImprimirAdvertenciaCambioModeloAsync("C56", "GN", "BLANCA"));

        var (destino, zpl) = Assert.Single(_printer.Enviados);
        Assert.Equal("tcp://10.0.0.50:9100", destino);
        Assert.Equal(AdvertenciaCambioModelo.Generar("C56", "GN", "BLANCA"), zpl);
    }

    [Fact]
    public async Task Cuenta_impresas_hoy_usuario_y_ayer()
    {
        _db.Seed(db =>
        {
            db.EtiquetasCocinas.Add(new EtiquetaCocina { Numero = 50, Fecha = "09/10/2026-08:00:00", IdProducto = "P1", Usuario = "L2-jperez" });
            db.EtiquetasCocinas.Add(new EtiquetaCocina { Numero = 51, Fecha = "09/10/2026-09:00:00", IdProducto = "P1", Usuario = "L1-otro" });
            db.EtiquetasCocinas.Add(new EtiquetaCocina { Numero = 52, Fecha = "09/10/2026-09:00:00", IdProducto = "P2", Usuario = "L2-jperez" });
        });

        var c = await Service().ContarImpresasAsync("P1");

        Assert.Equal(new ConteoImpresas(Hoy: 2, Usuario: 1, Ayer: 1), c);
    }
}
