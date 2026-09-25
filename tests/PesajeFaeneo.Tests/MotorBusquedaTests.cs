using PesajeFaeneo.Application.Catalogos.Busqueda;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MotorBusquedaTests
{
    [Fact]
    public void Buscar_AcentosYEñes_PenaEncuentraPena()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "AH", "ESTANCIA PEÑA")
        };

        var resultados = MotorBusqueda.Buscar(elementos, "pena");
        Assert.Single(resultados);
        Assert.Equal("ESTANCIA PEÑA", resultados[0].Descripcion);
    }

    [Fact]
    public void Buscar_CodigoExactoAntesQuePrefijo_OrdenaCodigoExactoPrimero()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "110", "CLIENTE 110"),
            new(2, "11", "CLIENTE 11"),
            new(3, "111", "CLIENTE 111")
        };

        var resultados = MotorBusqueda.Buscar(elementos, "11");
        Assert.Equal(3, resultados.Count);
        Assert.Equal("11", resultados[0].Codigo);
    }

    [Fact]
    public void Buscar_PrefijoDeCodigoAntesQueNombre()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "99", "11 CLIENTE POR NOMBRE"),
            new(2, "115", "OTRO CLIENTE")
        };

        var resultados = MotorBusqueda.Buscar(elementos, "11");
        Assert.Equal(2, resultados.Count);
        Assert.Equal("115", resultados[0].Codigo); // Rango 1 (prefijo código) antes de rango 2 (prefijo nombre)
    }

    [Fact]
    public void Buscar_MultiplesPalabrasRango3_EncuentraCoincidenciasYDescartaOtras()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "1", "JUAN PEREZ"),
            new(2, "2", "PEDRO JUAN PERALTA"),
            new(3, "3", "JUANA LOPEZ")
        };

        var resultados = MotorBusqueda.Buscar(elementos, "juan per");
        Assert.Equal(2, resultados.Count);
        Assert.Contains(resultados, r => r.Descripcion == "JUAN PEREZ");
        Assert.Contains(resultados, r => r.Descripcion == "PEDRO JUAN PERALTA");
        Assert.DoesNotContain(resultados, r => r.Descripcion == "JUANA LOPEZ");
    }

    [Fact]
    public void Buscar_PrefijoNombreAntesQueContiene_Rango2AntesQueRango4()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "1", "JUANA"),
            new(2, "2", "ANA MARIA")
        };

        var resultados = MotorBusqueda.Buscar(elementos, "ANA");
        Assert.Equal(2, resultados.Count);
        Assert.Equal("ANA MARIA", resultados[0].Descripcion); // Rango 2 (prefijo nombre)
        Assert.Equal("JUANA", resultados[1].Descripcion);     // Rango 4 (contiene)
    }

    [Fact]
    public void Buscar_TextoVacio_DevuelveVacio()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "1", "NOMBRE")
        };

        Assert.Empty(MotorBusqueda.Buscar(elementos, ""));
        Assert.Empty(MotorBusqueda.Buscar(elementos, "   "));
        Assert.Empty(MotorBusqueda.Buscar(elementos, null));
    }

    [Fact]
    public void Buscar_MaximoRespetado()
    {
        var elementos = Enumerable.Range(1, 30)
            .Select(i => new ElementoBusqueda(i, i.ToString(), $"CLIENTE {i}"))
            .ToList();

        var resultados = MotorBusqueda.Buscar(elementos, "CLIENTE", maximo: 5);
        Assert.Equal(5, resultados.Count);
    }

    [Fact]
    public void ResolverExacto_CodigoExacto_DevuelveElemento()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "110", "CLIENTE 110"),
            new(2, "11", "CLIENTE 11")
        };

        var res = MotorBusqueda.ResolverExacto(elementos, "11");
        Assert.NotNull(res);
        Assert.Equal("11", res.Codigo);
    }

    [Fact]
    public void ResolverExacto_ResultadoUnicoPorBusqueda_DevuelveElemento()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "110", "CLIENTE 110"),
            new(2, "220", "CLIENTE 220")
        };

        var res = MotorBusqueda.ResolverExacto(elementos, "11");
        Assert.NotNull(res);
        Assert.Equal("110", res.Codigo);
    }

    [Fact]
    public void ResolverExacto_Ambiguedad_DevuelveNull()
    {
        var elementos = new List<ElementoBusqueda>
        {
            new(1, "110", "CLIENTE 110"),
            new(2, "111", "CLIENTE 111")
        };

        var res = MotorBusqueda.ResolverExacto(elementos, "11");
        Assert.Null(res);
    }
}
