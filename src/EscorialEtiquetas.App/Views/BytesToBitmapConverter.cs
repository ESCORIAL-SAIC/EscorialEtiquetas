using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace EscorialEtiquetas.App.Views;

/// <summary>Muestra las imágenes guardadas en la base (byte[]) sin que los ViewModels dependan de Avalonia.</summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
            return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch (Exception)
        {
            return null; // imagen corrupta en la base: no se muestra
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
