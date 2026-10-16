using Avalonia.Controls;
using EscorialEtiquetas.App.ViewModels;

namespace EscorialEtiquetas.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow() => InitializeComponent();

    public LoginWindow(LoginViewModel vm) : this()
    {
        DataContext = vm;
        Opened += async (_, _) => await vm.InicializarAsync();
    }
}
