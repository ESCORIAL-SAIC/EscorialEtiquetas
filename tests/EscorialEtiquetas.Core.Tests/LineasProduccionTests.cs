using EscorialEtiquetas.Core.Configuracion;

namespace EscorialEtiquetas.Core.Tests;

public sealed class LineasProduccionTests
{
    [Fact]
    public void Cocinas_son_L1_L2_L3() =>
        Assert.Equal(["L1", "L2", "L3"], LineasProduccion.Para(Zona.Cocinas));

    [Fact]
    public void Termos_son_LAGA_LAEL_LAGE_LACA() =>
        Assert.Equal(["LAGA", "LAEL", "LAGE", "LACA"], LineasProduccion.Para(Zona.Termos));

    [Theory]
    [InlineData("L1", true)]
    [InlineData("L3", true)]
    [InlineData("LAGA", true)]
    [InlineData("LACA", true)]
    [InlineData("L4", false)]
    [InlineData("LAXX", false)]
    [InlineData("laga", false)]
    [InlineData("", false)]
    public void EsValida(string linea, bool esperado) =>
        Assert.Equal(esperado, LineasProduccion.EsValida(linea));
}
