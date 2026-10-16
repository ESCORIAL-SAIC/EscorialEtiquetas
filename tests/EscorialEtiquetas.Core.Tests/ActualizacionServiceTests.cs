using System.Net;
using System.Text;
using EscorialEtiquetas.Core.Servicios;

namespace EscorialEtiquetas.Core.Tests;

public sealed class ActualizacionServiceTests
{
    private const string Json = """
        [
          { "tag_name": "0.3.0", "html_url": "https://github.com/x/r/0.3.0", "draft": true, "prerelease": false },
          { "tag_name": "0.2.1-rc.10", "html_url": "https://github.com/x/r/0.2.1-rc.10", "draft": false, "prerelease": true },
          { "tag_name": "0.2.1-rc.2", "html_url": "https://github.com/x/r/0.2.1-rc.2", "draft": false, "prerelease": true },
          { "tag_name": "0.2.0", "html_url": "https://github.com/x/r/0.2.0", "draft": false, "prerelease": false },
          { "tag_name": "0.1.9", "html_url": "https://github.com/x/r/0.1.9", "draft": false, "prerelease": false }
        ]
        """;

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Pedidos { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Pedidos++;
            Assert.NotEmpty(request.Headers.UserAgent);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static (ActualizacionService, FakeHandler) Crear(string version, HttpStatusCode status = HttpStatusCode.OK, string body = Json)
    {
        var h = new FakeHandler(status, body);
        return (new ActualizacionService(new HttpClient(h), version), h);
    }

    [Theory]
    [InlineData("0.2.0", "0.1.9", 1)]
    [InlineData("0.2.0", "0.2.0-rc.5", 1)]
    [InlineData("0.2.0-rc.10", "0.2.0-rc.2", 1)]
    [InlineData("0.2.0-rc.2", "0.2.0-rc.10", -1)]
    [InlineData("0.2.0-rc.5", "0.2.0", -1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-rc.3", "1.0.0-rc.3", 0)]
    public void Comparar_respeta_semver(string a, string b, int esperado) =>
        Assert.Equal(esperado, Math.Sign(ActualizacionService.Comparar(a, b)));

    [Fact]
    public async Task Estable_no_ve_rc_ni_draft()
    {
        var (s, _) = Crear("0.1.9");
        var r = await s.BuscarAsync();
        Assert.Equal(new Release("0.2.0", "https://github.com/x/r/0.2.0"), r);
        Assert.Equal(r, s.Disponible);
    }

    [Fact]
    public async Task Estable_al_dia_devuelve_null()
    {
        var (s, _) = Crear("0.2.0");
        Assert.Null(await s.BuscarAsync());
        Assert.Null(s.Disponible);
    }

    [Fact]
    public async Task Rc_ve_rc_mas_nueva_comparando_numericamente()
    {
        var (s, _) = Crear("0.2.1-rc.2");
        var cambios = 0;
        s.DisponibleCambio += (_, _) => cambios++;
        Assert.Equal("0.2.1-rc.10", (await s.BuscarAsync())?.Version);
        Assert.Equal(1, cambios);
    }

    [Fact]
    public async Task Rc_ve_estable_mas_nueva()
    {
        var (s, _) = Crear("0.2.0-rc.5");
        Assert.Equal("0.2.1-rc.10", (await s.BuscarAsync())?.Version);
        var (s2, _) = Crear("0.2.0-rc.5", body: """[{ "tag_name": "0.2.0", "html_url": "https://github.com/x/r/0.2.0", "draft": false, "prerelease": false }]""");
        Assert.Equal("0.2.0", (await s2.BuscarAsync())?.Version);
    }

    [Fact]
    public async Task Dev_no_hace_request()
    {
        var (s, h) = Crear("0.0.0-dev");
        Assert.Null(await s.BuscarAsync());
        Assert.Equal(0, h.Pedidos);
    }

    [Fact]
    public async Task Error_http_lanza_y_no_toca_disponible()
    {
        var (s, _) = Crear("0.1.9", HttpStatusCode.Forbidden, "{}");
        await Assert.ThrowsAsync<HttpRequestException>(() => s.BuscarAsync());
        Assert.Null(s.Disponible);
    }
}
