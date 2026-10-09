using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Escorial.Etiquetas.App.Servicios;
using Escorial.Etiquetas.App.ViewModels;
using Escorial.Etiquetas.App.Views;

namespace Escorial.Etiquetas.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            try
            {
                var services = AppServices.Crear();
                var dialogos = new DialogService(services, () => desktop.Windows.LastOrDefault(w => w.IsActive) ?? desktop.MainWindow);
                desktop.MainWindow = services.Settings.UsaLogin
                    ? CrearLogin(desktop, services, dialogos)
                    : CrearPrincipal(services, dialogos);
            }
            catch (Exception ex)
            {
                desktop.MainWindow = DialogService.CrearMensaje("Error de configuración", ex.Message, esError: true, conCancelar: false);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindow CrearPrincipal(AppServices s, IDialogService dialogos) =>
        new(new MainViewModel(s.Settings, s.Etiquetas, s.Catalogo, s.Usuarios, s.Habilitaciones, dialogos));

    private static Window CrearLogin(IClassicDesktopStyleApplicationLifetime desktop, AppServices s, IDialogService dialogos)
    {
        var vm = new LoginViewModel(s.Settings, s.Usuarios, dialogos);
        var login = new LoginWindow(vm);
        vm.Ingreso += (_, _) =>
        {
            var principal = CrearPrincipal(s, dialogos);
            desktop.MainWindow = principal;
            principal.Show();
            login.Close();
        };
        return login;
    }
}
