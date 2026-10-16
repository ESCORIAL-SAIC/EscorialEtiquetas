using Avalonia.Controls;
using EscorialEtiquetas.App.ViewModels;

namespace EscorialEtiquetas.App.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow() => InitializeComponent();

    public ConfigWindow(ConfigViewModel vm) : this()
    {
        DataContext = vm;
        Closed += (_, _) => vm.Dispose();
    }
}
