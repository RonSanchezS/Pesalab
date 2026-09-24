using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ClasificadorErroresTests
{
    [Fact]
    public void Clasificar_ValidacionException_RetornaTipoValidacionSinLog()
    {
        var ex = new ValidacionException(new[]
        {
            new ErrorValidacion("Nombre", "Requerido"),
            new ErrorValidacion("Codigo", "Inválido")
        });

        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Validacion, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Null(resultado.CodigoIncidente);
        Assert.Contains("Nombre: Requerido", resultado.Mensaje);
        Assert.Contains("Codigo: Inválido", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_ReglaNegocioException_RetornaTipoNegocioSinLog()
    {
        var ex = new ReglaNegocioException("COD_01", "La res debe tener ambos cuartos.");
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Equal("La res debe tener ambos cuartos.", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_NoAutorizadoException_RetornaTipoNoAutorizado()
    {
        var ex = new NoAutorizadoException();
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.NoAutorizado, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Equal("No tiene permisos para esta acción.", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_ExcepcionInesperada_RetornaTipoInesperadoConCodigoYLog()
    {
        var ex = new InvalidOperationException("Falla inesperada en operación");
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Inesperado, resultado.Tipo);
        Assert.True(resultado.DebeRegistrarLog);
        Assert.NotNull(resultado.CodigoIncidente);
        Assert.Equal(8, resultado.CodigoIncidente.Length);
        Assert.Contains(resultado.CodigoIncidente, resultado.Mensaje);
    }
}
