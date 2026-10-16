using Avalonia.Controls;
using EscorialEtiquetas.App.ViewModels;

namespace EscorialEtiquetas.App.Views;

public partial class ImagenesWindow : Window
{
    public ImagenesWindow() => InitializeComponent();

    public ImagenesWindow(ImagenesViewModel vm) : this() => DataContext = vm;
}
