using Avalonia.Controls;
using Escorial.Etiquetas.App.ViewModels;

namespace Escorial.Etiquetas.App.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow() => InitializeComponent();

    public ConfigWindow(ConfigViewModel vm) : this() => DataContext = vm;
}
