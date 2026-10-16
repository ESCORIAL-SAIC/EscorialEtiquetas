using System.IO.Ports;
using EscorialEtiquetas.Core.Configuracion;

namespace EscorialEtiquetas.Core.Hardware;

public enum LedColor { Amarillo, Verde, Azul, Rojo }

/// <summary>
/// Sensor de distancia + LEDs por puerto serie. En Linux el puerto es /dev/ttyUSB0 o
/// /dev/ttyACM0 (el usuario tiene que estar en el grupo <c>dialout</c>).
/// En la app original esta función estaba deshabilitada; se porta para la impresión automática.
/// </summary>
public sealed class Arduino : IDisposable
{
    private readonly SerialPort _port;
    private readonly AppSettings _settings;

    public event Action<bool>? ObjetoDetectado;

    public Arduino(AppSettings settings)
    {
        _settings = settings;
        _port = new SerialPort(settings.ArduinoPort, settings.ArduinoBaudRate) { NewLine = "\n" };
    }

    public static string[] PuertosDisponibles() => SerialPort.GetPortNames();

    public void Abrir() => _port.Open();

    /// <summary>Pide la distancia cada 500 ms y dispara <see cref="ObjetoDetectado"/> en los flancos.</summary>
    public async Task DetectarAsync(CancellationToken ct)
    {
        var detectado = false;
        void OnData(object sender, SerialDataReceivedEventArgs e)
        {
            if (!int.TryParse(_port.ReadLine().Trim(), out var distancia))
                return;
            var ahora = DeteccionPorDistancia(distancia, _settings.DistanciaMaximaCm);
            if (ahora != detectado)
            {
                detectado = ahora;
                ObjetoDetectado?.Invoke(ahora);
            }
        }

        _port.DataReceived += OnData;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            do
            {
                _port.WriteLine(_settings.MedirDistancia);
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _port.DataReceived -= OnData;
        }
    }

    public static bool DeteccionPorDistancia(int distanciaCm, int maximaCm) => distanciaCm <= maximaCm;

    public void SetLed(LedColor color, bool encendido) => _port.WriteLine(ComandoLed(_settings, color, encendido));

    public static string ComandoLed(AppSettings s, LedColor color, bool encendido) => (color, encendido) switch
    {
        (LedColor.Amarillo, true) => s.LedAmarilloOn,
        (LedColor.Amarillo, false) => s.LedAmarilloOff,
        (LedColor.Verde, true) => s.LedVerdeOn,
        (LedColor.Verde, false) => s.LedVerdeOff,
        (LedColor.Azul, true) => s.LedAzulOn,
        (LedColor.Azul, false) => s.LedAzulOff,
        (LedColor.Rojo, true) => s.LedRojoOn,
        (LedColor.Rojo, false) => s.LedRojoOff,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    public void Dispose()
    {
        if (_port.IsOpen)
            _port.Close();
        _port.Dispose();
    }
}
