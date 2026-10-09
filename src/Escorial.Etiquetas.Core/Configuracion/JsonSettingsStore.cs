using System.Text.Json;

namespace Escorial.Etiquetas.Core.Configuracion;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>
/// Guarda la configuración en <c>%APPDATA%\EscorialEtiquetas\settings.json</c> (Windows)
/// o <c>~/.config/EscorialEtiquetas/settings.json</c> (Linux).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string FilePath { get; }

    public JsonSettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EscorialEtiquetas",
        "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return new AppSettings();

        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(stream, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Archivo corrupto: se arranca con valores por defecto, igual que Settings.settings.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
