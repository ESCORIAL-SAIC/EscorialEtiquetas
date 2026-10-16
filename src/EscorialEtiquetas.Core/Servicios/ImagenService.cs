using SkiaSharp;

namespace EscorialEtiquetas.Core.Servicios;

/// <summary>Recompresión de las fotos de los modelos (reemplaza System.Drawing, que solo anda en Windows).</summary>
public static class ImagenService
{
    public const int Calidad = 75;

    public static byte[] Comprimir(byte[] original, string nombreArchivo)
    {
        var formato = Path.GetExtension(nombreArchivo).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".png" => SKEncodedImageFormat.Png,
            var ext => throw new NotSupportedException($"Formato de imagen no soportado: '{ext}'. Usar JPG o PNG."),
        };

        using var bitmap = SKBitmap.Decode(original)
            ?? throw new InvalidDataException("El archivo no es una imagen válida.");
        using var data = bitmap.Encode(formato, Calidad);
        return data.ToArray();
    }
}
