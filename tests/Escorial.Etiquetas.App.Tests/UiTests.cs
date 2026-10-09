using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Escorial.Etiquetas.App.Views;

namespace Escorial.Etiquetas.App.Tests;

/// <summary>Ventanas reales renderizadas sin pantalla (sirve en CI y en Linux sin escritorio).</summary>
public sealed class UiTests : IDisposable
{
    private readonly Escenario _e = new();
    public void Dispose() => _e.Dispose();

    private static async Task Esperar(Func<bool> condicion)
    {
        for (var i = 0; i < 100 && !condicion(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Assert.True(condicion());
    }

    [AvaloniaFact]
    public async Task Ventana_principal_muestra_datos_e_imprime_con_click()
    {
        var w = new MainWindow(_e.Main());
        w.Show();

        var codigo = w.FindControl<TextBlock>("LblCodigo")!;
        await Esperar(() => codigo.Text == "Codigo: P1");
        Assert.Equal("C56", w.FindControl<ComboBox>("CmbModelo")!.SelectedItem?.ToString());

        var boton = w.FindControl<Button>("BtnImprimir")!;
        var centro = boton.TranslatePoint(new(boton.Bounds.Width / 2, boton.Bounds.Height / 2), w)!.Value;
        w.MouseDown(centro, MouseButton.Left);
        w.MouseUp(centro, MouseButton.Left);

        await Esperar(() => _e.Printer.Enviados.Count == 1);
        var serie = w.FindControl<TextBlock>("LblSerie")!;
        await Esperar(() => serie.Text == "Serie: 11");

        GuardarCaptura(w, "main-window.png");
    }

    [AvaloniaFact]
    public async Task Ventana_de_configuracion_se_renderiza()
    {
        var vm = _e.Config();
        await vm.InicializarAsync();
        var w = new ConfigWindow(vm);
        w.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("tcp://zebra:9100", w.FindControl<AutoCompleteBox>("TxtImpresora")!.Text);
        Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "IP BD: sql-cocinas");

        GuardarCaptura(w, "config-window.png");
    }

    /// <summary>Deja una captura en bin/.../capturas para revisar la UI sin abrir la app.</summary>
    private static void GuardarCaptura(Window w, string nombre)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = w.CaptureRenderedFrame();
        if (frame is null)
            return;
        var dir = Path.Combine(AppContext.BaseDirectory, "capturas");
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, nombre));
    }
}
