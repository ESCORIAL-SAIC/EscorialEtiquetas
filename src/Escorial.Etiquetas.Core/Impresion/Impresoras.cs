using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Escorial.Etiquetas.Core.Impresion;

/// <summary>Envía datos crudos (ZPL) a una impresora.</summary>
public interface IRawPrinter
{
    Task EnviarAsync(string destino, string datos, CancellationToken ct = default);
}

/// <summary>
/// Elige el mecanismo según el destino configurado:
/// <c>tcp://host:puerto</c> o <c>host:puerto</c> → socket directo (puerto 9100 de la Zebra);
/// cualquier otro nombre → cola del sistema (winspool en Windows, CUPS en Linux/macOS).
/// </summary>
public sealed class RawPrinter : IRawPrinter
{
    /// <summary>
    /// Igual que EscoBLL (Marshal.StringToCoTaskMemAnsi): un byte por carácter.
    /// Latin-1 coincide con Windows-1252 para los caracteres de las plantillas.
    /// </summary>
    public static Encoding Codificacion => Encoding.Latin1;

    public Task EnviarAsync(string destino, string datos, CancellationToken ct = default)
    {
        if (TcpRawPrinter.TryParse(destino, out var host, out var port))
            return TcpRawPrinter.EnviarAsync(host, port, datos, ct);

        if (OperatingSystem.IsWindows())
        {
            WindowsRawPrinter.Enviar(destino, datos);
            return Task.CompletedTask;
        }

        return CupsRawPrinter.EnviarAsync(destino, datos, ct);
    }

    /// <summary>Colas de impresión instaladas en el sistema, para sugerir en Configuración.</summary>
    public static async Task<IReadOnlyList<string>> ListarImpresorasAsync()
    {
        if (OperatingSystem.IsWindows())
            return WindowsRawPrinter.Listar();
        return await CupsRawPrinter.ListarAsync();
    }
}

public static class TcpRawPrinter
{
    public const int PuertoZebra = 9100;

    public static bool TryParse(string destino, out string host, out int port)
    {
        host = "";
        port = PuertoZebra;
        var s = destino.Trim();

        if (s.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            s = s["tcp://".Length..].TrimEnd('/');
            var idx = s.LastIndexOf(':');
            if (idx < 0)
            {
                host = s;
                return host.Length > 0;
            }
            host = s[..idx];
            return host.Length > 0 && int.TryParse(s[(idx + 1)..], out port);
        }

        // "host:puerto" sin esquema. No se aceptan nombres sin puerto para no
        // confundir una cola de impresión llamada "Zebra" con un host.
        var colon = s.LastIndexOf(':');
        if (colon > 0 && !s.Contains('\\') && !s.Contains('/') && int.TryParse(s[(colon + 1)..], out port))
        {
            host = s[..colon];
            return true;
        }
        return false;
    }

    public static async Task EnviarAsync(string host, int port, string datos, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(host, port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(RawPrinter.Codificacion.GetBytes(datos), timeout.Token);
        await stream.FlushAsync(timeout.Token);
    }
}

public static class CupsRawPrinter
{
    public static async Task EnviarAsync(string cola, string datos, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("lp")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(cola);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add("raw");

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo ejecutar 'lp' (¿CUPS instalado?).");
        await proc.StandardInput.BaseStream.WriteAsync(RawPrinter.Codificacion.GetBytes(datos), ct);
        proc.StandardInput.Close();
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"lp terminó con código {proc.ExitCode}: {await proc.StandardError.ReadToEndAsync(ct)}");
    }

    public static async Task<IReadOnlyList<string>> ListarAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("lpstat", "-e") { RedirectStandardOutput = true, UseShellExecute = false };
            using var proc = Process.Start(psi);
            if (proc is null)
                return [];
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return []; // CUPS no instalado
        }
    }
}

/// <summary>Equivalente a EscoBLL.RawPrinterHelper (winspool.drv).</summary>
[SupportedOSPlatform("windows")]
public static class WindowsRawPrinter
{
    public static IReadOnlyList<string> Listar() =>
        System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToList();

    public static void Enviar(string impresora, string datos)
    {
        var bytes = RawPrinter.Codificacion.GetBytes(datos);
        if (!OpenPrinter(impresora, out var hPrinter, IntPtr.Zero))
            throw new InvalidOperationException($"No se pudo abrir la impresora '{impresora}' (error {Marshal.GetLastWin32Error()}).");
        try
        {
            var di = new DOCINFOA { pDocName = "Etiqueta", pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, di))
                throw new InvalidOperationException($"StartDocPrinter falló (error {Marshal.GetLastWin32Error()}).");
            try
            {
                StartPagePrinter(hPrinter);
                var unmanaged = Marshal.AllocCoTaskMem(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                    if (!WritePrinter(hPrinter, unmanaged, bytes.Length, out _))
                        throw new InvalidOperationException($"WritePrinter falló (error {Marshal.GetLastWin32Error()}).");
                }
                finally
                {
                    Marshal.FreeCoTaskMem(unmanaged);
                }
                EndPagePrinter(hPrinter);
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private sealed class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
}
