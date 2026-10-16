using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using EscorialEtiquetas.App.Servicios;
using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Hardware;
using EscorialEtiquetas.Core.Impresion;
using EscorialEtiquetas.Core.Servicios;

namespace EscorialEtiquetas.App.ViewModels;

/// <summary>Configuración (ex FrmConfig).</summary>
public sealed partial class ConfigViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly LocalSecrets _secrets;
    private readonly UsuarioService _usuarios;
    private readonly IDialogService _dialogos;
    private readonly ActualizacionService _actualizaciones;
    private readonly Func<Task<IReadOnlyList<string>>> _listarImpresoras;

    public ConfigViewModel(
        AppSettings settings,
        ISettingsStore store,
        LocalSecrets secrets,
        UsuarioService usuarios,
        IDialogService dialogos,
        ActualizacionService actualizaciones,
        Func<Task<IReadOnlyList<string>>>? listarImpresoras = null)
    {
        _settings = settings;
        _store = store;
        _secrets = secrets;
        _usuarios = usuarios;
        _dialogos = dialogos;
        _actualizaciones = actualizaciones;
        // El chequeo periódico puede encontrar una versión con la ventana abierta.
        _actualizaciones.DisponibleCambio += OnDisponibleCambio;
        _listarImpresoras = listarImpresoras ?? RawPrinter.ListarImpresorasAsync;

        _impresora = settings.PrinterName;
        _tipo = settings.Zona == Zona.Termos ? "TERMOS" : "COCINAS";
        var linea = (settings.LineaProduccion ?? "").Trim().ToUpperInvariant();
        _linea = Lineas.Contains(linea) ? linea : "L1";
        _testing = settings.Testing;
        _imprimeAdvertencia = settings.ImprimeAdvertenciaCambioModelo;
        _usuarioManual = settings.UserManual;
        _passwordManual = settings.PasswordManual;

        _usaLogin = settings.UsaLogin;
        _arduinoPort = settings.ArduinoPort;
        _arduinoBaudRate = settings.ArduinoBaudRate;
        _medirDistancia = settings.MedirDistancia;
        _distanciaMaximaCm = settings.DistanciaMaximaCm;
        _varFecha = settings.VarFecha;
        _varSerie = settings.VarSerie;
        _caption = settings.Caption;
        _ledAmarilloOn = settings.LedAmarilloOn;
        _ledAmarilloOff = settings.LedAmarilloOff;
        _ledAzulOn = settings.LedAzulOn;
        _ledAzulOff = settings.LedAzulOff;
        _ledVerdeOn = settings.LedVerdeOn;
        _ledVerdeOff = settings.LedVerdeOff;
        _ledRojoOn = settings.LedRojoOn;
        _ledRojoOff = settings.LedRojoOff;
        _mapeoRutas = string.Join(Environment.NewLine, settings.MapeoRutas.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    public string Titulo => _settings.Caption;
    public IReadOnlyList<string> Tipos { get; } = ["COCINAS", "TERMOS"];
    public IReadOnlyList<string> Lineas { get; } = ["L1", "L2", "L3"];
    public ObservableCollection<string> Impresoras { get; } = [];
    public ObservableCollection<string> Usuarios { get; } = [];
    public ObservableCollection<string> PuertosSerie { get; } = [];

    /// <summary>Con login activo el usuario de impresión lo define el inicio de sesión.</summary>
    public bool UsuarioManualEditable => !_settings.UsaLogin;

    [ObservableProperty] private string _impresora;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IpBaseDatos))] private string _tipo;
    [ObservableProperty] private string _linea;
    [ObservableProperty] private bool _testing;
    [ObservableProperty] private bool _imprimeAdvertencia;
    [ObservableProperty] private string? _usuarioManual;
    [ObservableProperty] private string _passwordManual;

    [ObservableProperty] private bool _avanzadasHabilitadas;
    /// <summary>Se aplica al reiniciar la app: decide si arranca en la pantalla de login.</summary>
    [ObservableProperty] private bool _usaLogin;
    [ObservableProperty] private string _arduinoPort;
    [ObservableProperty] private int _arduinoBaudRate;
    [ObservableProperty] private string _medirDistancia;
    [ObservableProperty] private int _distanciaMaximaCm;
    [ObservableProperty] private string _varFecha;
    [ObservableProperty] private string _varSerie;
    [ObservableProperty] private string _caption;
    [ObservableProperty] private string _ledAmarilloOn;
    [ObservableProperty] private string _ledAmarilloOff;
    [ObservableProperty] private string _ledAzulOn;
    [ObservableProperty] private string _ledAzulOff;
    [ObservableProperty] private string _ledVerdeOn;
    [ObservableProperty] private string _ledVerdeOff;
    [ObservableProperty] private string _ledRojoOn;
    [ObservableProperty] private string _ledRojoOff;

    /// <summary>Una línea por mapeo: <c>\\servidor\carpeta=/mnt/carpeta</c>.</summary>
    [ObservableProperty] private string _mapeoRutas;

    public string VersionActual => "Versión actual: " + _actualizaciones.VersionActual;
    public bool HayActualizacion => _actualizaciones.Disponible is not null;
    public string TextoActualizacion => $"Nueva versión {_actualizaciones.Disponible?.Version} disponible";

    private void OnDisponibleCambio(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnDisponibleCambio(sender, e));
            return;
        }
        OnPropertyChanged(nameof(HayActualizacion));
        OnPropertyChanged(nameof(TextoActualizacion));
    }

    public void Dispose() => _actualizaciones.DisponibleCambio -= OnDisponibleCambio;

    public string IpBaseDatos => "IP BD: " + _secrets.SqlHost(Tipo == "TERMOS" ? Zona.Termos : Zona.Cocinas);

    public async Task InicializarAsync()
    {
        try
        {
            foreach (var u in await _usuarios.NombresAsync())
                Usuarios.Add(u);
            // El ComboBox necesita que el ítem exista antes de seleccionarlo.
            OnPropertyChanged(nameof(UsuarioManual));
        }
        catch (Exception ex)
        {
            await _dialogos.ErrorAsync($"No se pudieron cargar los usuarios: {ex.Message}");
        }

        foreach (var p in await _listarImpresoras())
            Impresoras.Add(p);
        foreach (var p in Arduino.PuertosDisponibles())
            PuertosSerie.Add(p);
    }

    [RelayCommand]
    private async Task VerificarUsuarioAsync()
    {
        if (await _usuarios.VerificarClaveAsync(UsuarioManual, PasswordManual))
            await _dialogos.InfoAsync("Usuario verificado correctamente");
        else
            await _dialogos.ErrorAsync("La clave es incorrecta");
    }

    [RelayCommand]
    private async Task AvanzadasAsync()
    {
        var clave = await _dialogos.PedirClaveAsync("Ingrese clave de administrador");
        if (clave is null)
            return;
        if (await _usuarios.VerificarClaveAsync("administrador", clave))
            AvanzadasHabilitadas = true;
        else
            await _dialogos.ErrorAsync("La clave es incorrecta");
    }

    [RelayCommand]
    private async Task BuscarActualizacionesAsync()
    {
        if (_actualizaciones.EsDesarrollo)
        {
            await _dialogos.InfoAsync("Versión de desarrollo: no se buscan actualizaciones");
            return;
        }
        try
        {
            var nueva = await _actualizaciones.BuscarAsync();
            await _dialogos.InfoAsync(nueva is null
                ? "Ya tenés la última versión"
                : $"Hay una nueva versión {nueva.Version} disponible");
        }
        catch (Exception ex)
        {
            await _dialogos.ErrorAsync($"No se pudo consultar GitHub: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task DescargarAsync() =>
        _actualizaciones.Disponible is { } r ? _dialogos.AbrirEnlaceAsync(r.Url) : Task.CompletedTask;

    [RelayCommand]
    private Task ImagenesAsync() => _dialogos.AbrirImagenesAsync();

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (!await _usuarios.VerificarClaveAsync(UsuarioManual, PasswordManual))
        {
            await _dialogos.ErrorAsync("La clave es incorrecta.\nNo se guardarán las configuraciones.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Impresora))
        {
            await _dialogos.ErrorAsync("Por favor, complete todos los campos antes de guardar las configuraciones.");
            return;
        }

        _settings.PrinterName = Impresora.Trim();
        _settings.Tipo = Tipo;
        _settings.LineaProduccion = Linea;
        _settings.Testing = Testing;
        _settings.ImprimeAdvertenciaCambioModelo = ImprimeAdvertencia;
        if (!_settings.UsaLogin)
        {
            _settings.UserManual = UsuarioManual ?? "";
            _settings.PasswordManual = PasswordManual;
        }

        if (AvanzadasHabilitadas)
        {
            _settings.UsaLogin = UsaLogin;
            _settings.ArduinoPort = ArduinoPort;
            _settings.ArduinoBaudRate = ArduinoBaudRate;
            _settings.MedirDistancia = MedirDistancia;
            _settings.DistanciaMaximaCm = DistanciaMaximaCm;
            _settings.VarFecha = VarFecha;
            _settings.VarSerie = VarSerie;
            _settings.Caption = Caption;
            _settings.LedAmarilloOn = LedAmarilloOn;
            _settings.LedAmarilloOff = LedAmarilloOff;
            _settings.LedAzulOn = LedAzulOn;
            _settings.LedAzulOff = LedAzulOff;
            _settings.LedVerdeOn = LedVerdeOn;
            _settings.LedVerdeOff = LedVerdeOff;
            _settings.LedRojoOn = LedRojoOn;
            _settings.LedRojoOff = LedRojoOff;
            _settings.MapeoRutas = ParsearMapeo(MapeoRutas);
        }

        _store.Save(_settings);
        await _dialogos.InfoAsync("Configuraciones guardadas correctamente");
    }

    internal static Dictionary<string, string> ParsearMapeo(string texto) =>
        texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2 && p[0].Length > 0 && p[1].Length > 0)
            .GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last()[1]);
}
