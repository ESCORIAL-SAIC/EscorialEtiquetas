using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Datos;
using EscorialEtiquetas.Core.Servicios;
using SkiaSharp;

namespace EscorialEtiquetas.Core.Tests;

public sealed class ServiciosTests : IDisposable
{
    private readonly SqliteDb _db = new();

    public ServiciosTests()
    {
        _db.Seed(db =>
        {
            db.Usuarios.Add(new Usuario { Id = 1, Nombre = "jperez", NombreCompleto = "Juan Pérez", Contrasena = "1234" });
            db.Usuarios.Add(new Usuario { Id = 2, Nombre = "administrador", NombreCompleto = "Admin", Contrasena = "admin" });
            db.Modelos.Add(new Modelo { Id = 1, Descripcion = "C56", Categoria = "COCINAS", TablaG = "G1", TablaE = "E1", Imagen = [1, 2, 3] });
            db.Modelos.Add(new Modelo { Id = 2, Descripcion = "T80", Categoria = "TERMOS", TablaG = "G2", TablaE = "E2" });
            db.Gases.Add(new Gas { Id = 1, Descripcion = "GN", Identificacion = "G1,G2" });
            db.Gases.Add(new Gas { Id = 2, Descripcion = "GE", Identificacion = "G2" });
            db.Equipamientos.Add(new Equipamiento { Id = 1, Descripcion = "BLANCA", Identificacion = "E1" });
            db.Equipamientos.Add(new Equipamiento { Id = 2, Descripcion = "INOX", Identificacion = "E1,E2" });
        });
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("jperez", "1234", true)]
    [InlineData("jperez", "mala", false)]
    [InlineData("nadie", "1234", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public async Task Verifica_clave(string? usuario, string? clave, bool esperado) =>
        Assert.Equal(esperado, await new UsuarioService(_db).VerificarClaveAsync(usuario, clave));

    [Fact]
    public async Task Lista_usuarios() =>
        Assert.Equal(["jperez", "administrador"], await new UsuarioService(_db).NombresAsync());

    [Fact]
    public async Task Modelos_filtrados_por_zona()
    {
        var settings = new AppSettings { Tipo = "COCINAS" };
        var svc = new CatalogoService(_db, settings);
        Assert.Equal(["C56"], (await svc.ModelosAsync()).Select(m => m.Descripcion));

        settings.Tipo = "TERMOS";
        Assert.Equal(["T80"], (await svc.ModelosAsync()).Select(m => m.Descripcion));
    }

    [Fact]
    public async Task Opciones_de_modelo_filtran_gas_y_equipamiento()
    {
        var o = await new CatalogoService(_db, new AppSettings()).OpcionesAsync(1);

        Assert.NotNull(o);
        Assert.Equal(["GN"], o.Gases.Select(g => g.Descripcion));
        Assert.Equal(["BLANCA", "INOX"], o.Equipamientos.Select(e => e.Descripcion));
        Assert.Equal([1, 2, 3], o.Imagen);
    }

    [Fact]
    public async Task Guarda_imagen_de_modelo()
    {
        var svc = new CatalogoService(_db, new AppSettings());
        await svc.GuardarImagenAsync(2, [9, 9]);
        Assert.Equal([9, 9], (await svc.OpcionesAsync(2))!.Imagen);
    }

    private sealed class FakeOrigen(params EstadoHabilitacion[] estados) : IHabilitacionOrigen
    {
        public Task<IReadOnlyList<EstadoHabilitacion>> ObtenerAsync(string? productoId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EstadoHabilitacion>>(estados);
    }

    [Fact]
    public async Task Sincroniza_habilitaciones()
    {
        var antes = new DateTime(2020, 1, 1);
        _db.Seed(db =>
        {
            db.HabilitacionProductos.Add(new HabilitacionProducto { ProductoId = "A", Habilitado = false, Actualizacion = antes });
            db.HabilitacionProductos.Add(new HabilitacionProducto { ProductoId = "B", Habilitado = true, Actualizacion = antes });
        });
        var reloj = new FixedTime(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var svc = new HabilitacionService(_db, new FakeOrigen(new("A", true), new("B", true), new("C", false)), reloj);

        await svc.ActualizarAsync();

        await using var db = _db.CreateDbContext();
        var h = db.HabilitacionProductos.ToDictionary(x => x.ProductoId);
        Assert.True(h["A"].Habilitado);
        Assert.NotEqual(antes, h["A"].Actualizacion);
        Assert.Equal(antes, h["B"].Actualizacion); // sin cambios, no se toca
        Assert.False(h["C"].Habilitado);
    }

    [Theory]
    [InlineData("foto.png")]
    [InlineData("foto.JPG")]
    public void Comprime_imagen(string nombre)
    {
        using var bmp = new SKBitmap(64, 64);
        bmp.Erase(SKColors.CornflowerBlue);
        using var png = bmp.Encode(SKEncodedImageFormat.Png, 100);

        var resultado = ImagenService.Comprimir(png.ToArray(), nombre);

        using var decodificada = SKBitmap.Decode(resultado);
        Assert.Equal(64, decodificada.Width);
    }

    [Fact]
    public void Formato_de_imagen_no_soportado() =>
        Assert.Throws<NotSupportedException>(() => ImagenService.Comprimir([1], "foto.bmp"));
}
