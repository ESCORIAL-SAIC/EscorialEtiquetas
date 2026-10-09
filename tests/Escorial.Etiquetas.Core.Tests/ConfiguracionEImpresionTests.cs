using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;
using Escorial.Etiquetas.Core.Configuracion;
using Escorial.Etiquetas.Core.Hardware;
using Escorial.Etiquetas.Core.Impresion;

namespace Escorial.Etiquetas.Core.Tests;

public sealed class ConfiguracionTests
{
    private const string Xml = """
        <secrets>
          <postgres><host>pg.local</host><database>DB</database><user>u</user><password>p</password></postgres>
          <sql>
            <cocinas><providerConnectionString>data source=10.0.0.1;initial catalog=Etiquetas;user id=a;password=b</providerConnectionString></cocinas>
            <termos><providerConnectionString>data source=10.0.0.2;initial catalog=Etiquetas;user id=a;password=b</providerConnectionString></termos>
          </sql>
        </secrets>
        """;

    [Fact]
    public void Lee_secrets_por_zona()
    {
        var s = LocalSecrets.Parse(XDocument.Parse(Xml))!;
        Assert.Equal("10.0.0.1", s.SqlHost(Zona.Cocinas));
        Assert.Equal("10.0.0.2", s.SqlHost(Zona.Termos));
        Assert.Contains("Host=pg.local", s.PostgresConnectionString(null));
    }

    [Fact]
    public void Secrets_formato_legacy_usa_la_misma_conexion_para_ambas_zonas()
    {
        var s = LocalSecrets.Parse(XDocument.Parse(
            "<secrets><sql><providerConnectionString>data source=legacy;user id=a</providerConnectionString></sql></secrets>"))!;
        Assert.Equal("legacy", s.SqlHost(Zona.Cocinas));
        Assert.Equal("legacy", s.SqlHost(Zona.Termos));
    }

    [Fact]
    public void Sin_credenciales_postgres_falla_y_sin_sql_informa_no_configurado()
    {
        var s = LocalSecrets.Parse(XDocument.Parse("<secrets><postgres><host>h</host></postgres></secrets>"))!;
        Assert.Throws<InvalidOperationException>(() => s.PostgresConnectionString("x"));
        Assert.Equal("(no configurado)", s.SqlHost(Zona.Cocinas));
    }

    [Fact]
    public void Postgres_usa_ip_publica_y_base_por_defecto()
    {
        var s = new LocalSecrets(null, null, "u", "p", null, null);
        var cs = s.PostgresConnectionString("1.2.3.4");
        Assert.Contains("Host=1.2.3.4", cs);
        Assert.Contains("Database=ESCORIAL", cs);
    }

    [Fact]
    public void Busca_secrets_en_la_primera_ruta_existente()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, LocalSecrets.FileName);
            File.WriteAllText(path, Xml);
            var s = LocalSecrets.Load([Path.Combine(dir.FullName, "no-existe.xml"), path]);
            Assert.Equal("pg.local", s!.PostgreHost);
            Assert.Null(LocalSecrets.Load([Path.Combine(dir.FullName, "no-existe.xml")]));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Settings_ida_y_vuelta_y_valores_por_defecto()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new JsonSettingsStore(Path.Combine(dir.FullName, "sub", "settings.json"));
            var defaults = store.Load();
            Assert.Equal("#NUM#", defaults.VarSerie);
            Assert.True(defaults.BloqueaProductos);
            Assert.Equal(Zona.Cocinas, defaults.Zona);

            defaults.Tipo = "TERMOS";
            defaults.Testing = true;
            defaults.MapeoRutas[@"\\srv\etq"] = "/mnt/etq";
            store.Save(defaults);

            var leido = store.Load();
            Assert.Equal(Zona.Termos, leido.Zona);
            Assert.True(leido.Testing);
            Assert.Equal("/mnt/etq", leido.MapeoRutas[@"\\srv\etq"]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Settings_corrupto_vuelve_a_defaults()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ esto no es json");
            Assert.Equal("Zebra", new JsonSettingsStore(path).Load().PrinterName);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class ImpresionTests
{
    [Theory]
    [InlineData("tcp://10.0.0.50:9100", true, "10.0.0.50", 9100)]
    [InlineData("tcp://zebra-l1", true, "zebra-l1", 9100)]
    [InlineData("10.0.0.50:6101", true, "10.0.0.50", 6101)]
    [InlineData("Zebra", false, "", 9100)]
    [InlineData(@"\\srv\Zebra", false, "", 9100)]
    public void Interpreta_destino_tcp(string destino, bool esTcp, string host, int port)
    {
        Assert.Equal(esTcp, TcpRawPrinter.TryParse(destino, out var h, out var p));
        if (esTcp)
        {
            Assert.Equal(host, h);
            Assert.Equal(port, p);
        }
    }

    [Fact]
    public async Task Envia_zpl_por_tcp_en_latin1()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var recibido = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ms = new MemoryStream();
            await client.GetStream().CopyToAsync(ms);
            return ms.ToArray();
        });

        await new RawPrinter().EnviarAsync($"tcp://127.0.0.1:{port}", "^XA^FDAñ^FS^XZ");

        var bytes = await recibido.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("^XA^FDAñ^FS^XZ"u8.Length - 1, bytes.Length); // ñ ocupa 1 byte, no 2
        Assert.Equal(0xF1, bytes[7]);
    }

    [Fact]
    public void Renderiza_plantilla()
    {
        var zpl = ZplTemplate.Render("^FD#NUM#^FS^FD#FECHA#^FS", "#NUM#", 123, "#FECHA#", "09/10/2026-14:30:05");
        Assert.Equal("^FD123^FS^FD09/10/2026^FS", zpl);
    }

    [Fact]
    public void Advertencia_genera_el_mismo_zpl_que_winforms()
    {
        var zpl = AdvertenciaCambioModelo.Generar("C56", "GN", "BLANCA");

        Assert.StartsWith(
            "^XA    ^FO40,100^A0N,100,100^FDC56^FS    ^FO40,220^A0N,100,100^FDGN^FS " +
            "   ^FO40,340^A0N,100,100^FDBLANCA^FS    ^FO40,30^GFA,95034,95034,94, ,lP0G1hW0G1G8S0", zpl);
        Assert.EndsWith(":G7GCoL0G7GFoL0:^FS ^XZ", zpl);
        // Logo completo (gráfico ^GFA de 32 KB) sin recortes
        Assert.Equal(32420, AdvertenciaCambioModelo.Generar("", "", "").Length);
    }

    [Theory]
    [InlineData(@"\\srv\etiquetas\cocinas\a.prn", "/mnt/etq/cocinas/a.prn")]
    [InlineData(@"\\SRV\ETIQUETAS\b.prn", "/mnt/etq/b.prn")]
    [InlineData(@"\\otro\x\c.prn", "//otro/x/c.prn")]
    public void Traduce_rutas_windows_en_linux(string ruta, string esperado)
    {
        var mapeo = new Dictionary<string, string> { [@"\\srv\etiquetas"] = "/mnt/etq/" };
        Assert.Equal(esperado, ArchivoPlantillaReader.TraducirRuta(ruta, mapeo, esWindows: false));
        Assert.Equal(ruta, ArchivoPlantillaReader.TraducirRuta(ruta, mapeo, esWindows: true));
    }
}

public sealed class ArduinoTests
{
    [Theory]
    [InlineData(LedColor.Amarillo, true, "b")]
    [InlineData(LedColor.Amarillo, false, "c")]
    [InlineData(LedColor.Azul, true, "d")]
    [InlineData(LedColor.Verde, false, "g")]
    [InlineData(LedColor.Rojo, true, "h")]
    public void Comandos_led_desde_configuracion(LedColor color, bool on, string esperado) =>
        Assert.Equal(esperado, Arduino.ComandoLed(new AppSettings(), color, on));

    [Theory]
    [InlineData(39, true)]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void Deteccion_por_distancia(int cm, bool esperado) =>
        Assert.Equal(esperado, Arduino.DeteccionPorDistancia(cm, 40));
}
