using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Escorial.Etiquetas.Core.Configuracion;

/// <summary>
/// Credenciales leídas de <c>secrets.local.xml</c>. Mismo formato que la app WinForms:
/// <code>
/// &lt;secrets&gt;
///   &lt;postgres&gt;&lt;host/&gt;&lt;database/&gt;&lt;user/&gt;&lt;password/&gt;&lt;/postgres&gt;
///   &lt;sql&gt;
///     &lt;cocinas&gt;&lt;providerConnectionString/&gt;&lt;/cocinas&gt;
///     &lt;termos&gt;&lt;providerConnectionString/&gt;&lt;/termos&gt;
///   &lt;/sql&gt;
/// &lt;/secrets&gt;
/// </code>
/// </summary>
public sealed record LocalSecrets(
    string? PostgreHost,
    string? PostgreDatabase,
    string? PostgreUser,
    string? PostgrePassword,
    string? SqlCocinas,
    string? SqlTermos)
{
    public const string FileName = "secrets.local.xml";

    /// <summary>
    /// Rutas donde se busca el archivo, en orden: variable de entorno
    /// <c>ESCORIAL_SECRETS</c>, junto al ejecutable, carpeta de configuración del usuario
    /// y carpetas superiores (útil al correr desde <c>bin/Debug/net10.0</c>).
    /// </summary>
    public static IEnumerable<string> DefaultCandidatePaths()
    {
        var env = Environment.GetEnvironmentVariable("ESCORIAL_SECRETS");
        if (!string.IsNullOrWhiteSpace(env))
            yield return env;

        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, FileName);
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EscorialEtiquetas",
            FileName);

        var dir = new DirectoryInfo(baseDir).Parent;
        for (var i = 0; i < 5 && dir is not null; i++, dir = dir.Parent)
            yield return Path.Combine(dir.FullName, FileName);
    }

    public static LocalSecrets? Load(IEnumerable<string>? candidatePaths = null)
    {
        var path = (candidatePaths ?? DefaultCandidatePaths()).FirstOrDefault(File.Exists);
        return path is null ? null : Parse(XDocument.Load(path));
    }

    public static LocalSecrets? Parse(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            return null;

        var pg = root.Element("postgres");
        var sql = root.Element("sql");

        var cocinas = (string?)sql?.Element("cocinas")?.Element("providerConnectionString");
        var termos = (string?)sql?.Element("termos")?.Element("providerConnectionString");

        // Formato legacy: un único providerConnectionString para ambas zonas.
        if (string.IsNullOrWhiteSpace(cocinas) && string.IsNullOrWhiteSpace(termos))
        {
            var legacy = (string?)sql?.Element("providerConnectionString");
            cocinas = termos = legacy;
        }

        return new LocalSecrets(
            (string?)pg?.Element("host"),
            (string?)pg?.Element("database"),
            (string?)pg?.Element("user"),
            (string?)pg?.Element("password"),
            NullIfBlank(cocinas),
            NullIfBlank(termos));
    }

    public string SqlConnectionString(Zona zona)
    {
        var cs = zona == Zona.Termos ? SqlTermos : SqlCocinas;
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                $"Falta sql/{(zona == Zona.Termos ? "termos" : "cocinas")}/providerConnectionString en '{FileName}'.");
        return cs;
    }

    /// <summary>Host del SQL Server de la zona, para mostrar en Configuración.</summary>
    public string SqlHost(Zona zona)
    {
        try
        {
            return new SqlConnectionStringBuilder(SqlConnectionString(zona)).DataSource;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            return "(no configurado)";
        }
    }

    public string PostgresConnectionString(string? fallbackHost)
    {
        if (string.IsNullOrWhiteSpace(PostgreUser) || string.IsNullOrWhiteSpace(PostgrePassword))
            throw new InvalidOperationException(
                $"Faltan credenciales de PostgreSQL en '{FileName}' (postgres/user y postgres/password).");

        return new NpgsqlConnectionStringBuilder
        {
            Host = string.IsNullOrWhiteSpace(PostgreHost) ? fallbackHost : PostgreHost,
            Database = string.IsNullOrWhiteSpace(PostgreDatabase) ? "ESCORIAL" : PostgreDatabase,
            Username = PostgreUser,
            Password = PostgrePassword,
        }.ConnectionString;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
