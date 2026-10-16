using Avalonia.Controls;
using EscorialEtiquetas.App.ViewModels;

namespace EscorialEtiquetas.App.Views;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private bool _cierreConfirmado;

    public MainWindow() => InitializeComponent(); // diseñador

    public MainWindow(MainViewModel vm) : this()
    {
        DataContext = vm;
        Opened += async (_, _) =>
        {
            _ = vm.VigilarActualizacionesAsync(_cts.Token); // no bloquea la carga
            await vm.InicializarAsync();
            _ = vm.VigilarNuevosRegistrosAsync(_cts.Token);
        };
        Closing += async (_, e) =>
        {
            if (_cierreConfirmado)
                return;
            e.Cancel = true;
            if (await vm.ConfirmarCierreAsync())
            {
                _cierreConfirmado = true;
                _cts.Cancel();
                Close();
            }
        };
    }
}
