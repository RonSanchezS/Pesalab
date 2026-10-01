using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using Xunit;

namespace PesajeFaeneo.Tests.Application.Utilitarios.Importacion;

public class SugeridorMapeoTests
{
    [Fact]
    public void Sugerir_ColumnasDbfGanaderos_MapeaMarcaNombreActivo()
    {
        var cols = new[] { "MARCA", "NOMBRE", "PESO", "FECHA", "ACTIVO" };
        var mapeo = SugeridorMapeo.Sugerir(TipoImportacion.Ganaderos, cols);

        Assert.Equal(0, mapeo[CamposImportacion.Marca]);
        Assert.Equal(1, mapeo[CamposImportacion.Nombre]);
        Assert.Equal(4, mapeo[CamposImportacion.Activo]);
    }

    [Fact]
    public void Sugerir_NombresConEspaciosYAcentos_MapeaCorrectamente()
    {
        // Ganaderos: "Nombre Ganadero" -> Nombre
        var colsGan = new[] { "MARCA", "Nombre Ganadero" };
        var mapeoGan = SugeridorMapeo.Sugerir(TipoImportacion.Ganaderos, colsGan);
        Assert.Equal(1, mapeoGan[CamposImportacion.Nombre]);

        // Clientes: "Dirección" -> Direccion
        var colsCli = new[] { "CODIGO", "NOMBRE", "Dirección" };
        var mapeoCli = SugeridorMapeo.Sugerir(TipoImportacion.Clientes, colsCli);
        Assert.Equal(2, mapeoCli[CamposImportacion.Direccion]);
    }

    [Fact]
    public void Sugerir_CabeceraLegadoPesajes_MapeaTodosLosCamposSoportados()
    {
        var cols = new[]
        {
            "Item", "Nro Bol", "Fecha", "N° Res", "Marca", "Cliente",
            "Dirección", "Izq", "Der", "Total Neto", "Tipo Corte"
        };

        var mapeo = SugeridorMapeo.Sugerir(TipoImportacion.Pesajes, cols);

        Assert.Equal(1, mapeo[CamposImportacion.NumeroBoleta]);
        Assert.Equal(2, mapeo[CamposImportacion.Fecha]);
        Assert.Null(mapeo[CamposImportacion.Hora]);
        Assert.Equal(4, mapeo[CamposImportacion.MarcaGanadero]);
        Assert.Equal(5, mapeo[CamposImportacion.CodigoCliente]);
        Assert.Equal(3, mapeo[CamposImportacion.Secuencia]);
        Assert.Equal(6, mapeo[CamposImportacion.Direccion]);
        Assert.Null(mapeo[CamposImportacion.ColorRaza]);
        Assert.Null(mapeo[CamposImportacion.Sexo]);
        Assert.Equal(10, mapeo[CamposImportacion.TipoDesposte]);
        Assert.Equal(7, mapeo[CamposImportacion.PesoIzquierdo]);
        Assert.Equal(8, mapeo[CamposImportacion.PesoDerecho]);
        Assert.Equal(9, mapeo[CamposImportacion.PesoTotal]);
        Assert.Null(mapeo[CamposImportacion.Observacion]);
        Assert.Null(mapeo[CamposImportacion.Estado]);
        Assert.Null(mapeo[CamposImportacion.MotivoAnulacion]);
    }

    [Fact]
    public void Sugerir_ColumnasConMismoAlias_UnaColumnaNuncaSeAsignaADosCampos()
    {
        var cols = new[] { "MARCA", "MARCA_2" };
        var mapeo = SugeridorMapeo.Sugerir(TipoImportacion.Pesajes, cols);

        Assert.Equal(0, mapeo[CamposImportacion.MarcaGanadero]);
        // Ningún otro campo debe tener el índice 0 ni el 1 si no coincide
        foreach (var (campo, idx) in mapeo)
        {
            if (campo != CamposImportacion.MarcaGanadero)
            {
                Assert.NotEqual(0, idx);
            }
        }
    }
}
