using System.Net.Http.Json;
using System.Text.Json;

namespace EscorialEtiquetas.Core.Servicios;

public sealed record Release(string Version, string Url);

/// <summary>
/// Busca versiones nuevas en GitHub Releases. Una versión actual prerelease (X.Y.Z-rc.N)
/// considera estables y rc; una estable solo estables. Con versión "-dev" no se consulta.
/// </summary>
public sealed class ActualizacionService(HttpClient http, string versionActual)
{
    public const string UrlReleases = "https://api.github.com/repos/ESCORIAL-SAIC/EscorialEtiquetas/releases?per_page=20";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private sealed record GhRelease(string? TagName, string? HtmlUrl, bool Draft, bool Prerelease);

    public string VersionActual { get; } = versionActual;

    public bool EsDesarrollo => VersionActual.Contains("-dev", StringComparison.OrdinalIgnoreCase);

    /// <summary>Última versión nueva encontrada; la comparten la pantalla principal y Configuración.</summary>
    public Release? Disponible { get; private set; }

    public event EventHandler? DisponibleCambio;

    /// <summary>
    /// Devuelve la release más nueva del canal o null si está al día (o es versión de desarrollo).
    /// Lanza ante errores de red, HTTP o formato: el llamador decide si mostrarlos.
    /// </summary>
    public async Task<Release?> BuscarAsync(CancellationToken ct = default)
    {
        if (EsDesarrollo)
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, UrlReleases);
        req.Headers.UserAgent.ParseAdd("EscorialEtiquetas"); // GitHub rechaza pedidos sin User-Agent
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var releases = await resp.Content.ReadFromJsonAsync<List<GhRelease>>(Json, ct) ?? [];

        var nueva = Elegir(releases, VersionActual);
        if (nueva != Disponible)
        {
            Disponible = nueva;
            DisponibleCambio?.Invoke(this, EventArgs.Empty);
        }
        return nueva;
    }

    private static Release? Elegir(IEnumerable<GhRelease> releases, string actual)
    {
        var aceptaPre = actual.Contains('-');
        Release? mejor = null;
        foreach (var r in releases)
        {
            var version = r.TagName?.Trim().TrimStart('v', 'V');
            if (r.Draft || version is null || !EsValida(version))
                continue;
            if ((r.Prerelease || version.Contains('-')) && !aceptaPre)
                continue;
            // Solo se abren enlaces https: el contenido viene de la red.
            if (!Uri.TryCreate(r.HtmlUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                continue;
            if (mejor is null || Comparar(version, mejor.Version) > 0)
                mejor = new Release(version, url.ToString());
        }
        return mejor is not null && Comparar(mejor.Version, actual) > 0 ? mejor : null;
    }

    private static bool EsValida(string version) => Version.TryParse(version.Split('-', 2)[0], out _);

    /// <summary>
    /// Compara SemVer X.Y.Z[-pre]: a igual núcleo la estable gana a la prerelease y los
    /// segmentos numéricos de la prerelease se comparan como números (rc.10 &gt; rc.2).
    /// </summary>
    public static int Comparar(string a, string b)
    {
        var pa = a.Split('-', 2);
        var pb = b.Split('-', 2);
        var nucleo = Version.Parse(pa[0]).CompareTo(Version.Parse(pb[0]));
        if (nucleo != 0)
            return nucleo;
        if (pa.Length == 1 || pb.Length == 1)
            return pb.Length - pa.Length; // sin prerelease = mayor

        var sa = pa[1].Split('.');
        var sb = pb[1].Split('.');
        for (var i = 0; i < Math.Min(sa.Length, sb.Length); i++)
        {
            var c = int.TryParse(sa[i], out var na) && int.TryParse(sb[i], out var nb)
                ? na.CompareTo(nb)
                : string.CompareOrdinal(sa[i], sb[i]);
            if (c != 0)
                return Math.Sign(c);
        }
        return sa.Length.CompareTo(sb.Length);
    }
}
