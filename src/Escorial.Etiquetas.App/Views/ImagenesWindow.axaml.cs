using Avalonia.Controls;
using Escorial.Etiquetas.App.ViewModels;

namespace Escorial.Etiquetas.App.Views;

public partial class ImagenesWindow : Window
{
    public ImagenesWindow() => InitializeComponent();

    public ImagenesWindow(ImagenesViewModel vm) : this() => DataContext = vm;
}
