using EscorialEtiquetas.Core.Datos;
using Microsoft.EntityFrameworkCore;

namespace EscorialEtiquetas.Core.Servicios;

public sealed class UsuarioService(IDbContextFactory<EtiquetasDbContext> dbFactory)
{
    public async Task<IReadOnlyList<string>> NombresAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Usuarios.OrderBy(u => u.Id).Select(u => u.Nombre).ToListAsync(ct);
    }

    /// <summary>
    /// Igual que Functions.VerificaClave: compara contra la tabla Users
    /// (las contraseñas están en texto plano en la base).
    /// </summary>
    public async Task<bool> VerificarClaveAsync(string? usuario, string? clave, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(usuario) || clave is null)
            return false;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Usuarios.AnyAsync(u => u.Nombre == usuario && u.Contrasena == clave, ct);
    }
}
