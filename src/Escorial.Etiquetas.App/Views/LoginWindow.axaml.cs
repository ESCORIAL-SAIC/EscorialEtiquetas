using Avalonia.Controls;
using Escorial.Etiquetas.App.ViewModels;

namespace Escorial.Etiquetas.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow() => InitializeComponent();

    public LoginWindow(LoginViewModel vm) : this()
    {
        DataContext = vm;
        Opened += async (_, _) => await vm.InicializarAsync();
    }
}
