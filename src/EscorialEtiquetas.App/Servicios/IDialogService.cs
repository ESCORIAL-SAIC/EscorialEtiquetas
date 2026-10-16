namespace EscorialEtiquetas.App.Servicios;

/// <summary>
/// Mensajes y ventanas que abren los ViewModels. Reemplaza a la clase Message (MessageBox)
/// y permite testear los ViewModels sin interfaz.
/// </summary>
public interface IDialogService
{
    Task InfoAsync(string mensaje);
    Task ErrorAsync(string mensaje);
    Task<bool> ConfirmarAsync(string mensaje);
    Task<string?> PedirClaveAsync(string mensaje);
    Task<string?> ElegirImagenAsync();
    Task AbrirConfiguracionAsync();
    Task AbrirImagenesAsync();
}
