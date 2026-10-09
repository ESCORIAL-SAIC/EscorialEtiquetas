using Escorial.Etiquetas.App.ViewModels;
using Escorial.Etiquetas.Core.Configuracion;

namespace Escorial.Etiquetas.App.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    [Fact]
    public async Task Al_iniciar_carga_modelo_opciones_y_datos()
    {
        var vm = _e.Main();
        await vm.InicializarAsync();

        Assert.Equal("C56", Assert.Single(vm.Modelos).Descripcion);
        Assert.Equal("GN", vm.GasSeleccionado?.Descripcion);
        Assert.Equal("BLANCA", vm.EquipamientoSeleccionado?.Descripcion);
        Assert.Equal("Codigo: P1", vm.CodigoProducto);
        Assert.Equal("Serie: 10", vm.Serie);
        Assert.Equal("Etiquetas impresas hoy: 1", vm.TextoImpresasHoy);
        Assert.Null(vm.Estado);
    }

    [Fact]
    public async Task Imprimir_envia_a_la_impresora_y_actualiza_la_serie()
    {
        var vm = _e.Main();
        await vm.InicializarAsync();

        await vm.ImprimirCommand.ExecuteAsync(null);

        Assert.Empty(_e.Dialogos.Errores);
        Assert.Equal(("tcp://zebra:9100", "^XA^FD11^FS^XZ"), Assert.Single(_e.Printer.Enviados));
        Assert.Equal("Serie: 11", vm.Serie);
        Assert.Equal("Etiquetas impresas hoy: 2", vm.TextoImpresasHoy);
    }

    [Fact]
    public async Task Imprimir_con_clave_invalida_no_imprime()
    {
        _e.Settings.PasswordManual = "mala";
        var vm = _e.Main();
        await vm.InicializarAsync();

        await vm.ImprimirCommand.ExecuteAsync(null);

        Assert.Empty(_e.Printer.Enviados);
        Assert.Contains("inválidos", Assert.Single(_e.Dialogos.Errores));
    }

    [Fact]
    public async Task Producto_no_habilitado_muestra_el_mensaje_original()
    {
        _e.Db.Seed(db => db.HabilitacionProductos.RemoveRange(db.HabilitacionProductos));
        var vm = _e.Main();
        await vm.InicializarAsync();

        await vm.ImprimirCommand.ExecuteAsync(null);

        Assert.Empty(_e.Printer.Enviados);
        Assert.StartsWith("Producto no habilitado", Assert.Single(_e.Dialogos.Errores));
    }

    [Fact]
    public async Task Al_volver_de_configuracion_recarga_la_zona()
    {
        var vm = _e.Main();
        await vm.InicializarAsync();
        _e.Settings.Tipo = "TERMOS";

        await vm.ConfiguracionCommand.ExecuteAsync(null);

        Assert.Equal(1, _e.Dialogos.ConfiguracionAbierta);
        Assert.Equal("T80", Assert.Single(vm.Modelos).Descripcion);
        Assert.Equal("Codigo: T1", vm.CodigoProducto);
        Assert.Equal("Serie: 0", vm.Serie);
    }
}

public sealed class AdvertenciaCambioModeloTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    public AdvertenciaCambioModeloTests()
    {
        // Segundo modelo y segundo gas para poder cambiar la selección.
        _e.Db.Seed(db =>
        {
            db.Modelos.Add(new Escorial.Etiquetas.Core.Datos.Modelo { Id = 3, Descripcion = "C60", Categoria = "COCINAS", TablaG = "G1", TablaE = "E1" });
            db.Gases.Add(new Escorial.Etiquetas.Core.Datos.Gas { Id = 2, Descripcion = "GE", Identificacion = "G1" });
        });
    }

    private List<string> Avisos() => _e.Printer.Enviados.Select(x => x.Datos).Where(d => d.Contains("^GFA")).ToList();

    [Fact]
    public async Task Apagada_por_defecto_no_imprime_al_cambiar_modelo()
    {
        var vm = _e.Main();
        await vm.InicializarAsync();

        vm.ModeloSeleccionado = vm.Modelos.Single(m => m.Descripcion == "C60");
        await vm.ActualizarDatosAsync();

        Assert.Empty(_e.Printer.Enviados);
    }

    [Fact]
    public async Task Encendida_imprime_un_solo_aviso_por_cambio_del_operario()
    {
        _e.Settings.ImprimeAdvertenciaCambioModelo = true;
        var vm = _e.Main();

        await vm.InicializarAsync();
        Assert.Empty(Avisos()); // la selección inicial no es un cambio del operario

        vm.ModeloSeleccionado = vm.Modelos.Single(m => m.Descripcion == "C60");
        await Esperar(() => Avisos().Count >= 1);
        var aviso = Assert.Single(Avisos()); // y no uno por cada combo que se recarga
        Assert.Contains("^FDC60^FS", aviso);
        Assert.Contains("^FDGN^FS", aviso);
        Assert.Contains("^FDBLANCA^FS", aviso);

        vm.GasSeleccionado = vm.Gases.Single(g => g.Descripcion == "GE");
        await Esperar(() => Avisos().Count >= 2);
        Assert.Contains("^FDGE^FS", Avisos()[1]);

        await vm.ConfiguracionCommand.ExecuteAsync(null); // recarga al volver de Configuración
        Assert.Equal(2, Avisos().Count);
    }

    private static async Task Esperar(Func<bool> condicion)
    {
        for (var i = 0; i < 100 && !condicion(); i++)
            await Task.Delay(10);
        Assert.True(condicion());
    }
}

public sealed class ConfigViewModelTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    [Fact]
    public async Task Carga_valores_actuales()
    {
        var vm = _e.Config();
        await vm.InicializarAsync();

        Assert.Equal("tcp://zebra:9100", vm.Impresora);
        Assert.Equal("COCINAS", vm.Tipo);
        Assert.Equal(["Zebra_L1", "Zebra_L2"], vm.Impresoras);
        Assert.Equal(["jperez", "administrador"], vm.Usuarios);
        Assert.Equal("IP BD: sql-cocinas", vm.IpBaseDatos);

        vm.Tipo = "TERMOS";
        Assert.Equal("IP BD: sql-termos", vm.IpBaseDatos);
    }

    [Fact]
    public async Task Guardar_con_clave_correcta_persiste()
    {
        var vm = _e.Config();
        vm.Tipo = "TERMOS";
        vm.Linea = "L3";
        vm.Testing = true;
        vm.Impresora = "Zebra_L2";
        vm.ImprimeAdvertencia = true;

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Equal(1, _e.Store.Guardados);
        Assert.Equal(Zona.Termos, _e.Settings.Zona);
        Assert.Equal("L3", _e.Settings.LineaProduccion);
        Assert.True(_e.Settings.Testing);
        Assert.True(_e.Settings.ImprimeAdvertenciaCambioModelo);
        Assert.Equal("Zebra_L2", _e.Settings.PrinterName);
        Assert.Equal("Configuraciones guardadas correctamente", Assert.Single(_e.Dialogos.Infos));
    }

    [Fact]
    public async Task Guardar_con_clave_incorrecta_no_persiste()
    {
        var vm = _e.Config();
        vm.PasswordManual = "mala";
        vm.Tipo = "TERMOS";

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Equal(0, _e.Store.Guardados);
        Assert.Equal(Zona.Cocinas, _e.Settings.Zona);
        Assert.Single(_e.Dialogos.Errores);
    }

    [Fact]
    public async Task Avanzadas_solo_se_guardan_si_se_desbloquean()
    {
        var vm = _e.Config();
        vm.VarSerie = "#SERIE#";
        await vm.GuardarCommand.ExecuteAsync(null);
        Assert.Equal("#NUM#", _e.Settings.VarSerie);

        _e.Dialogos.RespuestaClave = "mala";
        await vm.AvanzadasCommand.ExecuteAsync(null);
        Assert.False(vm.AvanzadasHabilitadas);

        _e.Dialogos.RespuestaClave = "admin";
        await vm.AvanzadasCommand.ExecuteAsync(null);
        Assert.True(vm.AvanzadasHabilitadas);

        vm.MapeoRutas = "\\\\srv\\etq = /mnt/etq\nbasura\n";
        await vm.GuardarCommand.ExecuteAsync(null);
        Assert.Equal("#SERIE#", _e.Settings.VarSerie);
        Assert.Equal("/mnt/etq", Assert.Single(_e.Settings.MapeoRutas).Value);
    }
}

public sealed class LoginViewModelTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    [Fact]
    public async Task Ingreso_correcto_define_usuario_de_impresion()
    {
        _e.Settings.UserManual = "";
        var vm = new LoginViewModel(_e.Settings, _e.Usuarios, _e.Dialogos);
        await vm.InicializarAsync();
        var ingreso = false;
        vm.Ingreso += (_, _) => ingreso = true;

        vm.Usuario = "administrador";
        vm.Clave = "x";
        await vm.IngresarCommand.ExecuteAsync(null);
        Assert.False(ingreso);
        Assert.Equal("Clave incorrecta", Assert.Single(_e.Dialogos.Errores));

        vm.Clave = "admin";
        await vm.IngresarCommand.ExecuteAsync(null);
        Assert.True(ingreso);
        Assert.Equal("administrador", _e.Settings.UserManual);
    }
}
