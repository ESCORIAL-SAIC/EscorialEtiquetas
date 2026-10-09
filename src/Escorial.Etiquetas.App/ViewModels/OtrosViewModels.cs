using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Escorial.Etiquetas.App.Servicios;
using Escorial.Etiquetas.Core.Configuracion;
using Escorial.Etiquetas.Core.Servicios;

namespace Escorial.Etiquetas.App.ViewModels;

/// <summary>Inicio de sesión (ex FrmLogin). Solo se usa con <c>UsaLogin = true</c>.</summary>
public sealed partial class LoginViewModel(AppSettings settings, UsuarioService usuarios, IDialogService dialogos) : ObservableObject
{
    public ObservableCollection<string> Usuarios { get; } = [];

    [ObservableProperty] private string? _usuario;
    [ObservableProperty] private string _clave = "";

    public event EventHandler? Ingreso;

    public async Task InicializarAsync()
    {
        try
        {
            foreach (var u in await usuarios.NombresAsync())
                Usuarios.Add(u);
            Usuario = Usuarios.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await dialogos.ErrorAsync($"No se pudieron cargar los usuarios: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task IngresarAsync()
    {
        if (!await usuarios.VerificarClaveAsync(Usuario, Clave))
        {
            await dialogos.ErrorAsync("Clave incorrecta");
            return;
        }
        // Igual que el original: el usuario de la sesión no se persiste.
        settings.UserManual = Usuario!;
        settings.PasswordManual = Clave;
        Ingreso?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Carga de fotos de modelos (ex FrmImg).</summary>
public sealed partial class ImagenesViewModel(CatalogoService catalogo, IDialogService dialogos) : ObservableObject
{
    public ObservableCollection<Opcion> Modelos { get; } = [];

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmarCommand))] private Opcion? _modeloSeleccionado;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmarCommand))] private string? _ruta;
    [ObservableProperty] private byte[]? _vistaPrevia;

    public async Task InicializarAsync()
    {
        foreach (var m in await catalogo.ModelosAsync())
            Modelos.Add(m);
        ModeloSeleccionado = Modelos.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ExaminarAsync()
    {
        var ruta = await dialogos.ElegirImagenAsync();
        if (ruta is null)
            return;
        Ruta = ruta;
        VistaPrevia = await File.ReadAllBytesAsync(ruta);
    }

    private bool PuedeConfirmar() => ModeloSeleccionado is not null && !string.IsNullOrEmpty(Ruta);

    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarAsync()
    {
        try
        {
            var comprimida = ImagenService.Comprimir(await File.ReadAllBytesAsync(Ruta!), Ruta!);
            await catalogo.GuardarImagenAsync(ModeloSeleccionado!.Id, comprimida);
            VistaPrevia = comprimida;
            await dialogos.InfoAsync("Imagen comprimida y cargada correctamente");
        }
        catch (Exception ex)
        {
            await dialogos.ErrorAsync(ex.Message);
        }
    }
}
