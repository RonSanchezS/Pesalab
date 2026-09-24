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

    [Fact]
    public void Clasificar_ConcurrenciaException_RetornaTipoNegocioConMensajeFijoSinLog()
    {
        var ex = new ConcurrenciaException("mensaje técnico interno, no debe mostrarse");
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Equal("El registro fue modificado por otro usuario. Vuelva a cargarlo.", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_EntidadNoEncontradaException_RetornaTipoNegocioSinLog()
    {
        var ex = new EntidadNoEncontradaException("Ganadero", 99);
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Equal(ex.Message, resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_ConflictoException_RetornaTipoNegocioSinLog()
    {
        var ex = new ConflictoException("Ya existe un ganadero con esa marca.");
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Equal("Ya existe un ganadero con esa marca.", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_DomainExceptionGenerica_RetornaTipoNegocioSinLog()
    {
        // Cualquier DomainException que no tenga un caso más específico (no ValidacionException,
        // ConcurrenciaException, ConflictoException, EntidadNoEncontradaException, ReglaNegocioException
        // ni NoAutorizadoException) debe tratarse como un aviso de negocio, no como error inesperado.
        var ex = new DomainException("Regla de dominio no contemplada por un tipo específico.");
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Null(resultado.CodigoIncidente);
        Assert.Equal(ex.Message, resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_OperationCanceledException_RetornaTipoCanceladoSinLogNiMensaje()
    {
        var ex = new OperationCanceledException();
        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Cancelado, resultado.Tipo);
        Assert.False(resultado.DebeRegistrarLog);
        Assert.Null(resultado.CodigoIncidente);
    }

    [Fact]
    public void Clasificar_TargetInvocationEnvolviendoValidacion_DesenvuelveYClasificaComoValidacion()
    {
        var interna = new ValidacionException("Marca", "Requerida");
        var ex = new System.Reflection.TargetInvocationException(interna);

        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Validacion, resultado.Tipo);
        Assert.Contains("Marca: Requerida", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_AggregateConUnaSolaInterna_SeDesenvuelveYClasificaLaInterna()
    {
        var interna = new ReglaNegocioException("COD_02", "Regla violada.");
        var ex = new AggregateException(interna);

        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Negocio, resultado.Tipo);
        Assert.Equal("Regla violada.", resultado.Mensaje);
    }

    [Fact]
    public void Clasificar_AggregateConVariasInternas_NoSeDesenvuelveYEsInesperada()
    {
        // Con más de una excepción interna no hay una única causa "raíz": se clasifica el
        // AggregateException tal cual (como error inesperado), en vez de ocultar las demás.
        var ex = new AggregateException(
            new ReglaNegocioException("COD_03", "Primera falla."),
            new ReglaNegocioException("COD_04", "Segunda falla."));

        var resultado = ClasificadorErrores.Clasificar(ex);

        Assert.Equal(TipoErrorClasificado.Inesperado, resultado.Tipo);
        Assert.True(resultado.DebeRegistrarLog);
    }
}
