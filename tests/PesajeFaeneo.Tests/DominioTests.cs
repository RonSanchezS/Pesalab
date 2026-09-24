using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DominioTests
{
    [Fact]
    public void Enums_ValoresNumericosFijos_CoincidenConEspecificacion()
    {
        Assert.Equal(1, (int)Rol.Administrador);
        Assert.Equal(2, (int)Rol.Supervisor);
        Assert.Equal(3, (int)Rol.Operador);

        Assert.Equal(1, (int)TipoDesposte.MediaRes);
        Assert.Equal(2, (int)TipoDesposte.ResCompleta);

        Assert.Equal(1, (int)TipoConexion.Serial);
        Assert.Equal(2, (int)TipoConexion.Tcp);
        Assert.Equal(3, (int)TipoConexion.Simulador);

        Assert.Equal(1, (int)EstadoPesaje.Vigente);
        Assert.Equal(2, (int)EstadoPesaje.Anulado);
    }

    [Fact]
    public void ValidacionException_ConDosErrores_MuestraAmbosEnMensaje()
    {
        var errores = new List<ErrorValidacion>
        {
            new("Marca", "La marca no puede estar vacía."),
            new("Nombre", "El nombre es obligatorio.")
        };

        var exception = new ValidacionException(errores);

        Assert.Equal(2, exception.Errores.Count);
        Assert.Contains("Marca: La marca no puede estar vacía.", exception.Message);
        Assert.Contains("Nombre: El nombre es obligatorio.", exception.Message);
    }
}
