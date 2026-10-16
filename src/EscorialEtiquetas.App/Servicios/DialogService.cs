using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EscorialEtiquetas.App.ViewModels;
using EscorialEtiquetas.App.Views;

namespace EscorialEtiquetas.App.Servicios;

public sealed class DialogService(AppServices services, Func<Window?> owner) : IDialogService
{
    private string Titulo => services.Settings.Caption;

    public Task InfoAsync(string mensaje) => MostrarAsync(mensaje, esError: false, conCancelar: false);

    public Task ErrorAsync(string mensaje) => MostrarAsync(mensaje, esError: true, conCancelar: false);

    public Task<bool> ConfirmarAsync(string mensaje) => MostrarAsync(mensaje, esError: false, conCancelar: true);

    public async Task<string?> PedirClaveAsync(string mensaje)
    {
        var caja = new TextBox { PasswordChar = '•', Width = 260 };
        var ok = new Button { Content = "Aceptar", IsDefault = true, Classes = { "primario" } };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true };
        var w = Ventana(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = mensaje },
                caja,
                Botones(ok, cancelar),
            },
        });
        w.Title = Titulo;
        ok.Click += (_, _) => w.Close(caja.Text ?? "");
        cancelar.Click += (_, _) => w.Close(null);
        w.Opened += (_, _) => caja.Focus();
        return await MostrarModal<string?>(w);
    }

    public async Task<string?> ElegirImagenAsync()
    {
        var top = owner();
        if (top is null)
            return null;
        var archivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir imagen",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Imágenes") { Patterns = ["*.jpg", "*.jpeg", "*.png"] }],
        });
        return archivos.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task AbrirConfiguracionAsync()
    {
        var vm = new ConfigViewModel(services.Settings, services.SettingsStore, services.Secrets, services.Usuarios, this);
        var w = new ConfigWindow(vm);
        await vm.InicializarAsync();
        await MostrarModal<object?>(w);
    }

    public async Task AbrirImagenesAsync()
    {
        var vm = new ImagenesViewModel(services.Catalogo, this);
        var w = new ImagenesWindow(vm);
        await vm.InicializarAsync();
        await MostrarModal<object?>(w);
    }

    private async Task<bool> MostrarAsync(string mensaje, bool esError, bool conCancelar) =>
        await MostrarModal<bool?>(CrearMensaje(Titulo, mensaje, esError, conCancelar)) ?? false;

    private async Task<T?> MostrarModal<T>(Window w)
    {
        var top = owner();
        if (top is null || !top.IsVisible)
        {
            var tcs = new TaskCompletionSource<T?>();
            w.Closed += (_, _) => tcs.TrySetResult(default);
            w.Show();
            return await tcs.Task;
        }
        return await w.ShowDialog<T?>(top);
    }

    /// <summary>Equivalente a MessageBox: texto + Aceptar (y Cancelar si se pide confirmación).</summary>
    public static Window CrearMensaje(string titulo, string mensaje, bool esError, bool conCancelar)
    {
        var ok = new Button { Content = "Aceptar", IsDefault = true, MinWidth = 90, Classes = { "primario" } };
        var botones = conCancelar
            ? Botones(ok, new Button { Content = "Cancelar", IsCancel = true, MinWidth = 90 })
            : Botones(ok);
        var w = Ventana(new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = mensaje,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420,
                    Foreground = esError ? Brushes.Firebrick : Brushes.Black,
                    FontSize = 15,
                },
                botones,
            },
        });
        w.Title = titulo;
        ok.Click += (_, _) => w.Close(true);
        foreach (var b in botones.Children.OfType<Button>().Where(b => b.IsCancel))
            b.Click += (_, _) => w.Close(false);
        return w;
    }

    private static Window Ventana(Control contenido) => new()
    {
        Content = new Border { Padding = new Avalonia.Thickness(20), Child = contenido },
        SizeToContent = SizeToContent.WidthAndHeight,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ShowInTaskbar = false,
    };

    private static StackPanel Botones(params Button[] botones)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.AddRange(botones);
        return panel;
    }
}
