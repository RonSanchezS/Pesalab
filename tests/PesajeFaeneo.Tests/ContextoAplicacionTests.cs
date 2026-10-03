using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// No se instancia <see cref="ContextoAplicacion"/>: su constructor muestra el inicio de sesión.
/// </summary>
public class ContextoAplicacionTests
{
    private static EstadoPuestaEnMarcha Estado(bool completa) =>
        new(completa, true, "FRIGORIFICO X", TipoConexion.Serial, true, 0, 0);

    [Fact]
    public void DebeMostrarAsistente_SinPermiso_NoSeMuestra()
    {
        Assert.False(ContextoAplicacion.DebeMostrarAsistente(false, Estado(false)));
    }

    [Fact]
    public void DebeMostrarAsistente_SinEstado_NoSeMuestra()
    {
        Assert.False(ContextoAplicacion.DebeMostrarAsistente(true, null));
    }

    [Fact]
    public void DebeMostrarAsistente_PuestaEnMarchaCompleta_NoSeMuestra()
    {
        Assert.False(ContextoAplicacion.DebeMostrarAsistente(true, Estado(true)));
    }

    [Fact]
    public void DebeMostrarAsistente_ConPermisoEIncompleta_SeMuestra()
    {
        Assert.True(ContextoAplicacion.DebeMostrarAsistente(true, Estado(false)));
    }
}
