using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using EscorialEtiquetas.Core.Servicios;

namespace EscorialEtiquetas.App.Tests;

/// <summary>GitHub falso: nada de red real.</summary>
internal sealed class FakeGitHub(HttpStatusCode status = HttpStatusCode.OK, string? body = null) : HttpMessageHandler
{
    public const string UrlNueva = "https://github.com/x/r/releases/tag/9.0.0";
    public const string JsonNueva = $$"""[{ "tag_name": "9.0.0", "html_url": "{{UrlNueva}}", "draft": false, "prerelease": false }]""";

    public int Pedidos;
    public string? Body { get; set; } = body;
    public HttpStatusCode Status { get; set; } = status;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Pedidos);
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body ?? JsonNueva, Encoding.UTF8, "application/json") });
    }

    public static (ActualizacionService, FakeGitHub) Servicio(string version, HttpStatusCode status = HttpStatusCode.OK, string? body = null)
    {
        var h = new FakeGitHub(status, body);
        return (new ActualizacionService(new HttpClient(h), version), h);
    }
}

public sealed class MainViewModelActualizacionesTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    private static async Task Esperar(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion())
        {
            Assert.True(DateTime.UtcNow < limite, "Timeout esperando la condición");
            await Task.Delay(10);
        }
    }

    [AvaloniaFact]
    public async Task Version_nueva_pregunta_una_vez_y_abre_el_enlace_si_acepta()
    {
        var (svc, http) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => _e.Dialogos.EnlacesAbiertos.Count == 1);
        cts.Cancel();
        await tarea;

        Assert.Equal(FakeGitHub.UrlNueva, Assert.Single(_e.Dialogos.EnlacesAbiertos));
        Assert.Contains("9.0.0", Assert.Single(_e.Dialogos.Confirmaciones));
        Assert.Equal(1, http.Pedidos);
        Assert.Empty(_e.Dialogos.Errores);
    }

    [AvaloniaFact]
    public async Task Si_no_acepta_no_abre_el_enlace()
    {
        _e.Dialogos.RespuestaConfirmar = false;
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => _e.Dialogos.Confirmaciones.Count == 1);
        await Task.Delay(100);
        cts.Cancel();
        await tarea;

        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
    }

    [AvaloniaFact]
    public async Task Hay_version_nueva_marca_el_boton_Configuracion_con_alerta()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Main(svc);
        Assert.False(vm.HayActualizacion);
        Assert.Equal("Configuración", vm.TextoConfiguracion);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => vm.HayActualizacion);
        cts.Cancel();
        await tarea;

        Assert.Equal("Configuración (!)", vm.TextoConfiguracion);
    }

    [AvaloniaFact]
    public async Task Al_dia_no_pregunta_ni_marca()
    {
        var (svc, http) = FakeGitHub.Servicio("9.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => http.Pedidos == 1);
        await Task.Delay(100);
        cts.Cancel();
        await tarea;

        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.False(vm.HayActualizacion);
        Assert.Equal("Configuración", vm.TextoConfiguracion);
    }

    [AvaloniaFact]
    public async Task Error_HTTP_no_muestra_cartel_ni_error_ni_marca_y_el_loop_sigue_vivo()
    {
        var (svc, http) = FakeGitHub.Servicio("1.0.0", HttpStatusCode.Forbidden);
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => http.Pedidos == 1);
        await Task.Delay(100);
        Assert.False(tarea.IsCompleted); // el error no tumba la vigilancia
        cts.Cancel();
        await tarea; // termina sin lanzar

        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.Empty(_e.Dialogos.Errores);
        Assert.Empty(_e.Dialogos.Infos);
        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
        Assert.False(vm.HayActualizacion);
        Assert.Null(vm.Estado);
    }

    [AvaloniaFact]
    public async Task Error_de_red_no_rompe()
    {
        var svc = new ActualizacionService(new HttpClient(new ThrowingHandler()), "1.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Task.Delay(150);
        Assert.False(tarea.IsCompleted);
        cts.Cancel();
        await tarea;

        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.Empty(_e.Dialogos.Errores);
    }

    [AvaloniaFact]
    public async Task Version_dev_no_hace_pedidos()
    {
        var (svc, http) = FakeGitHub.Servicio("0.0.0-dev");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await tarea;

        Assert.Equal(0, http.Pedidos);
        Assert.Empty(_e.Dialogos.Confirmaciones);
    }

    [AvaloniaFact]
    public async Task Cancelar_antes_de_empezar_termina_sin_lanzar_ni_cartel()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await vm.VigilarActualizacionesAsync(cts.Token);

        Assert.Empty(_e.Dialogos.Errores);
        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
    }

    [AvaloniaFact]
    public async Task Al_terminar_la_vigilancia_se_desuscribe_del_servicio()
    {
        var (svc, http) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();
        var tarea = vm.VigilarActualizacionesAsync(cts.Token);
        await Esperar(() => vm.HayActualizacion);
        cts.Cancel();
        await tarea;

        // Al día: si siguiera suscripto, la marca se apagaría.
        http.Body = "[]";
        await svc.BuscarAsync();
        Assert.Null(svc.Disponible);
        Assert.True(vm.HayActualizacion);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("sin red");
    }

    // --- Chequeo periódico: intervalo corto, espera por polling (sin Delay fijo) ---

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);

    private static async Task EsperarHasta(Func<bool> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(15); // generoso: solo se agota si hay un bug
        while (!condicion())
        {
            Assert.True(DateTime.UtcNow < limite, $"Timeout esperando: {que}");
            await Task.Delay(10);
        }
    }

    [AvaloniaFact]
    public async Task Tick_con_version_nueva_pone_la_marca_sin_cartel_ni_enlace()
    {
        var (svc, http) = FakeGitHub.Servicio("9.0.0"); // al día en el arranque
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token, Tick);
        await EsperarHasta(() => http.Pedidos >= 2, "al menos un tick tras el arranque");
        Assert.False(vm.HayActualizacion);
        Assert.Equal("Configuración", vm.TextoConfiguracion);

        http.Body = FakeGitHub.JsonNueva.Replace("9.0.0", "9.1.0"); // sale una versión nueva
        await EsperarHasta(() => vm.HayActualizacion, "marca (!) tras el tick");
        cts.Cancel();
        await tarea;

        Assert.Equal("Configuración (!)", vm.TextoConfiguracion);
        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
        Assert.Empty(_e.Dialogos.Infos);
        Assert.Empty(_e.Dialogos.Errores);
    }

    [AvaloniaFact]
    public async Task Tick_con_error_no_rompe_ni_muestra_nada_y_el_siguiente_tick_funciona()
    {
        var (svc, http) = FakeGitHub.Servicio("9.0.0");
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        http.Status = HttpStatusCode.InternalServerError;
        var tarea = vm.VigilarActualizacionesAsync(cts.Token, Tick);
        await EsperarHasta(() => http.Pedidos >= 3, "arranque y ticks con error");
        Assert.False(tarea.IsCompleted); // el error no tumba el loop
        Assert.False(vm.HayActualizacion);
        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.Empty(_e.Dialogos.Errores);
        Assert.Null(vm.Estado);

        http.Status = HttpStatusCode.OK;
        http.Body = FakeGitHub.JsonNueva.Replace("9.0.0", "9.1.0");
        await EsperarHasta(() => vm.HayActualizacion, "recuperación en un tick posterior");
        cts.Cancel();
        await tarea;

        Assert.Empty(_e.Dialogos.Confirmaciones);
        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
    }

    [AvaloniaFact]
    public async Task Tick_vuelve_a_apagar_la_marca_si_ya_esta_al_dia()
    {
        var (svc, http) = FakeGitHub.Servicio("1.0.0"); // nueva desde el arranque
        _e.Dialogos.RespuestaConfirmar = false;
        var vm = _e.Main(svc);
        using var cts = new CancellationTokenSource();

        var tarea = vm.VigilarActualizacionesAsync(cts.Token, Tick);
        await EsperarHasta(() => vm.HayActualizacion, "marca en el arranque");
        http.Body = "[]";
        await EsperarHasta(() => !vm.HayActualizacion, "marca apagada tras el tick");
        cts.Cancel();
        await tarea;

        Assert.Single(_e.Dialogos.Confirmaciones); // el cartel salió solo en el arranque
    }
}

public sealed class ConfigViewModelActualizacionesTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    [AvaloniaFact]
    public async Task Buscar_con_version_nueva_informa_cual_y_activa_la_alerta()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Config(svc);

        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        Assert.Equal("Hay una nueva versión 9.0.0 disponible", Assert.Single(_e.Dialogos.Infos));
        Assert.True(vm.HayActualizacion);
        Assert.Equal("Nueva versión 9.0.0 disponible", vm.TextoActualizacion);
    }

    [AvaloniaFact]
    public async Task Buscar_al_dia_informa_que_ya_tiene_la_ultima()
    {
        var (svc, _) = FakeGitHub.Servicio("9.0.0");
        var vm = _e.Config(svc);

        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        Assert.Equal("Ya tenés la última versión", Assert.Single(_e.Dialogos.Infos));
        Assert.False(vm.HayActualizacion);
        Assert.Empty(_e.Dialogos.Errores);
    }

    [AvaloniaFact]
    public async Task Buscar_con_error_HTTP_muestra_error_y_no_lanza()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0", HttpStatusCode.Forbidden);
        var vm = _e.Config(svc);

        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        Assert.StartsWith("No se pudo consultar GitHub:", Assert.Single(_e.Dialogos.Errores));
        Assert.Empty(_e.Dialogos.Infos);
        Assert.False(vm.HayActualizacion);
    }

    [AvaloniaFact]
    public async Task Buscar_con_JSON_invalido_muestra_error()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0", body: "no es json");
        var vm = _e.Config(svc);

        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        Assert.StartsWith("No se pudo consultar GitHub:", Assert.Single(_e.Dialogos.Errores));
    }

    [AvaloniaFact]
    public async Task Buscar_en_dev_responde_sin_pedir_a_la_red()
    {
        var (svc, http) = FakeGitHub.Servicio("0.0.0-dev");
        var vm = _e.Config(svc);

        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        Assert.Equal("Versión de desarrollo: no se buscan actualizaciones", Assert.Single(_e.Dialogos.Infos));
        Assert.Equal(0, http.Pedidos);
    }

    [AvaloniaFact]
    public async Task Descargar_abre_la_url_de_la_release()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Config(svc);
        await vm.BuscarActualizacionesCommand.ExecuteAsync(null);

        await vm.DescargarCommand.ExecuteAsync(null);

        Assert.Equal(FakeGitHub.UrlNueva, Assert.Single(_e.Dialogos.EnlacesAbiertos));
    }

    [AvaloniaFact]
    public async Task Descargar_sin_version_nueva_no_abre_nada()
    {
        var (svc, _) = FakeGitHub.Servicio("9.0.0");
        var vm = _e.Config(svc);

        await vm.DescargarCommand.ExecuteAsync(null);

        Assert.Empty(_e.Dialogos.EnlacesAbiertos);
    }

    [AvaloniaFact]
    public async Task HayActualizacion_reacciona_a_DisponibleCambio_y_notifica()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Config(svc);
        var cambios = new List<string?>();
        vm.PropertyChanged += (_, a) => cambios.Add(a.PropertyName);
        Assert.False(vm.HayActualizacion);

        await svc.BuscarAsync(); // chequeo automático: sin cartel

        Assert.True(vm.HayActualizacion);
        Assert.Contains(nameof(vm.HayActualizacion), cambios);
        Assert.Contains(nameof(vm.TextoActualizacion), cambios);
        Assert.Empty(_e.Dialogos.Infos);
        Assert.Empty(_e.Dialogos.Confirmaciones);
    }

    [AvaloniaFact]
    public async Task Tras_Dispose_ya_no_notifica_cambios()
    {
        var (svc, _) = FakeGitHub.Servicio("1.0.0");
        var vm = _e.Config(svc);
        var cambios = new List<string?>();
        vm.PropertyChanged += (_, a) => cambios.Add(a.PropertyName);
        vm.Dispose();

        await svc.BuscarAsync();

        Assert.DoesNotContain(nameof(vm.HayActualizacion), cambios);
    }

    [AvaloniaFact]
    public void Muestra_la_version_actual()
    {
        var (svc, _) = FakeGitHub.Servicio("1.2.3-rc.4");
        Assert.Equal("Versión actual: 1.2.3-rc.4", _e.Config(svc).VersionActual);
    }
}
