using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using EscorialEtiquetas.App.Servicios;
using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Servicios;

namespace EscorialEtiquetas.App.ViewModels;

/// <summary>Pantalla principal (ex FrmInicio).</summary>
public sealed partial class MainViewModel(
    AppSettings settings,
    EtiquetaService etiquetas,
    CatalogoService catalogo,
    UsuarioService usuarios,
    HabilitacionService habilitaciones,
    ActualizacionService actualizaciones,
    IDialogService dialogos) : ObservableObject
{
    private bool _cargandoOpciones;
    private bool _recargandoModelos;
    private Task _cargaOpciones = Task.CompletedTask;
    private int _ultimoSerieVisto;

    public string Titulo => settings.Caption;

    public ObservableCollection<Opcion> Modelos { get; } = [];
    public ObservableCollection<Opcion> Gases { get; } = [];
    public ObservableCollection<Opcion> Equipamientos { get; } = [];

    [ObservableProperty] private Opcion? _modeloSeleccionado;
    [ObservableProperty] private Opcion? _gasSeleccionado;
    [ObservableProperty] private Opcion? _equipamientoSeleccionado;
    [ObservableProperty] private byte[]? _imagen;

    [ObservableProperty] private string _codigoProducto = "";
    [ObservableProperty] private string _serie = "";
    [ObservableProperty] private ConteoImpresas _conteo = new(0, 0, 0);

    /// <summary>Error de conexión u otro problema visible en la pantalla.</summary>
    [ObservableProperty] private string? _estado;

    /// <summary>Marca "(!)" en el botón cuando hay una versión nueva (el detalle está en Configuración).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TextoConfiguracion))] private bool _hayActualizacion;

    public string TextoConfiguracion => HayActualizacion ? "Configuración (!)" : "Configuración";

    public string TextoImpresasHoy => $"Etiquetas impresas hoy: {Conteo.Hoy}";
    public string TextoImpresasUsuario => $"Etiquetas impresas usuario: {Conteo.Usuario}";
    public string TextoImpresasAyer => $"Etiquetas impresas ayer: {Conteo.Ayer}";

    partial void OnConteoChanged(ConteoImpresas value)
    {
        OnPropertyChanged(nameof(TextoImpresasHoy));
        OnPropertyChanged(nameof(TextoImpresasUsuario));
        OnPropertyChanged(nameof(TextoImpresasAyer));
    }

    public async Task InicializarAsync()
    {
        _ = SincronizarHabilitacionesAsync();
        await RecargarModelosAsync();
    }

    /// <summary>
    /// Al arrancar busca una versión nueva y ofrece abrir su página; después repite cada
    /// <paramref name="intervalo"/> (30 min por defecto) sin cartel (solo la marca "(!)" y la alerta de Configuración). Termina con <paramref name="ct"/>.
    /// </summary>
    public async Task VigilarActualizacionesAsync(CancellationToken ct, TimeSpan? intervalo = null)
    {
        actualizaciones.DisponibleCambio += OnDisponibleCambio;
        try
        {
            try
            {
                var nueva = await actualizaciones.BuscarAsync(ct);
                if (nueva is not null && await dialogos.ConfirmarAsync($"Hay una nueva versión {nueva.Version} disponible. ¿Abrir la página de descarga?"))
                    await dialogos.AbrirEnlaceAsync(nueva.Url);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Console.Error.WriteLine($"No se pudo buscar actualizaciones: {ex.Message}");
            }

            using var timer = new PeriodicTimer(intervalo ?? TimeSpan.FromMinutes(30));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await actualizaciones.BuscarAsync(ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Sin red o GitHub caído: se reintenta en el próximo tick.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            actualizaciones.DisponibleCambio -= OnDisponibleCambio;
        }
    }

    private void OnDisponibleCambio(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
            HayActualizacion = actualizaciones.Disponible is not null;
        else
            Dispatcher.UIThread.Post(() => HayActualizacion = actualizaciones.Disponible is not null);
    }

    private async Task SincronizarHabilitacionesAsync()
    {
        try
        {
            await Task.Run(() => habilitaciones.ActualizarAsync());
        }
        catch (Exception ex)
        {
            // Igual que el original: si Calipso no responde se sigue con lo que hay en la base.
            Console.Error.WriteLine($"No se pudo sincronizar habilitaciones: {ex.Message}");
        }
    }

    /// <summary>Recarga los modelos de la zona actual (al iniciar y al volver de Configuración).</summary>
    public async Task RecargarModelosAsync()
    {
        // Selección programática: no cuenta como cambio de modelo del operario.
        _recargandoModelos = true;
        try
        {
            OnPropertyChanged(nameof(Titulo));
            var modelos = await catalogo.ModelosAsync();
            Modelos.Clear();
            foreach (var m in modelos)
                Modelos.Add(m);
            Estado = null;
            var anterior = ModeloSeleccionado;
            ModeloSeleccionado = Modelos.FirstOrDefault();
            if (ReferenceEquals(anterior, ModeloSeleccionado) || ModeloSeleccionado is null)
                await ActualizarDatosAsync();
            await _cargaOpciones;
        }
        catch (Exception ex)
        {
            Estado = $"Sin conexión a la base: {ex.Message}";
        }
        finally
        {
            _recargandoModelos = false;
        }
    }

    partial void OnModeloSeleccionadoChanged(Opcion? value) =>
        _cargaOpciones = CargarOpcionesAsync(value, avisarCambio: !_recargandoModelos);

    partial void OnGasSeleccionadoChanged(Opcion? value)
    {
        if (!_cargandoOpciones)
            _ = CambioDelOperarioAsync();
    }

    partial void OnEquipamientoSeleccionadoChanged(Opcion? value)
    {
        if (!_cargandoOpciones)
            _ = CambioDelOperarioAsync();
    }

    private async Task CambioDelOperarioAsync()
    {
        await ActualizarDatosAsync();
        await ImprimirAdvertenciaCambioModeloAsync();
    }

    /// <summary>
    /// Una sola etiqueta de aviso por cambio del operario (el original podía imprimir
    /// varias porque cada combo disparaba la suya al recargarse).
    /// </summary>
    private async Task ImprimirAdvertenciaCambioModeloAsync()
    {
        if (ModeloSeleccionado is null || GasSeleccionado is null || EquipamientoSeleccionado is null)
            return;
        try
        {
            await etiquetas.ImprimirAdvertenciaCambioModeloAsync(
                ModeloSeleccionado.Descripcion, GasSeleccionado.Descripcion, EquipamientoSeleccionado.Descripcion);
        }
        catch (Exception ex)
        {
            // Se informa en el banner para no frenar al operario con un diálogo en cada cambio.
            Estado = $"No se pudo imprimir el aviso de cambio de modelo: {ex.Message}";
        }
    }

    private async Task CargarOpcionesAsync(Opcion? modelo, bool avisarCambio)
    {
        _cargandoOpciones = true;
        try
        {
            Gases.Clear();
            Equipamientos.Clear();
            Imagen = null;
            if (modelo is not null && await catalogo.OpcionesAsync(modelo.Id) is { } opciones)
            {
                foreach (var g in opciones.Gases)
                    Gases.Add(g);
                foreach (var e in opciones.Equipamientos)
                    Equipamientos.Add(e);
                Imagen = opciones.Imagen;
            }
            GasSeleccionado = Gases.FirstOrDefault();
            EquipamientoSeleccionado = Equipamientos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
        }
        finally
        {
            _cargandoOpciones = false;
        }
        await ActualizarDatosAsync();
        if (avisarCambio)
            await ImprimirAdvertenciaCambioModeloAsync();
    }

    /// <summary>Código de producto, último número de serie y contadores (ex UpdateLabels + ObtenerCocinasImpresas).</summary>
    public async Task ActualizarDatosAsync()
    {
        try
        {
            var producto = await etiquetas.BuscarProductoAsync(
                ModeloSeleccionado?.Descripcion ?? "", GasSeleccionado?.Descripcion ?? "", EquipamientoSeleccionado?.Descripcion ?? "");
            _ultimoSerieVisto = await etiquetas.UltimoSerieAsync();
            CodigoProducto = $"Codigo: {producto?.Id}";
            Serie = $"Serie: {_ultimoSerieVisto}";
            Conteo = await etiquetas.ContarImpresasAsync(producto?.Id);
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
        }
    }

    /// <summary>
    /// Refresca la pantalla cuando otra estación imprime (ex CheckNewReg): consulta el
    /// último número de serie una vez por segundo.
    /// </summary>
    public async Task VigilarNuevosRegistrosAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    if (await etiquetas.UltimoSerieAsync(ct) > _ultimoSerieVisto)
                        await ActualizarDatosAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Corte de red momentáneo: se reintenta en el próximo tick.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task ImprimirAsync()
    {
        if (!await usuarios.VerificarClaveAsync(settings.UserManual, settings.PasswordManual))
        {
            await dialogos.ErrorAsync("Usuario o clave de impresión inválidos.\nRevisar la Configuración.");
            return;
        }

        try
        {
            var resultado = await etiquetas.ImprimirAsync(
                ModeloSeleccionado?.Descripcion ?? "", GasSeleccionado?.Descripcion ?? "", EquipamientoSeleccionado?.Descripcion ?? "");
            switch (resultado)
            {
                case ResultadoImpresion.NoHabilitado:
                    await dialogos.ErrorAsync("Producto no habilitado para su impresión.\nEn caso de error contactar a Ing. de Producto");
                    break;
                case ResultadoImpresion.ProductoNoEncontrado:
                    await dialogos.ErrorAsync("No se encontró el código de producto para la combinación elegida.");
                    break;
            }
        }
        catch (Exception ex)
        {
            await dialogos.ErrorAsync($"Error al imprimir: {ex.Message}");
        }
        await ActualizarDatosAsync();
    }

    [RelayCommand]
    private async Task ConfiguracionAsync()
    {
        await dialogos.AbrirConfiguracionAsync();
        await RecargarModelosAsync();
    }

    public Task<bool> ConfirmarCierreAsync() => dialogos.ConfirmarAsync("Desea cerrar la aplicación?");
}
