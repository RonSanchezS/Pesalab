using System.Text;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class LectorCsvTests
{
    static LectorCsvTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly LectorCsv _lector = new();

    [Fact]
    public void Leer_PuntoYComaYComillas_LeeColumnasValoresYFilaDos()
    {
        var csv = "Marca;Nombre;Fecha\nAB;\"PEREZ; JUAN\";01/02/2020\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var tabla = _lector.Leer(datos, null, "HUELLA1");

        Assert.Equal(3, tabla.Columnas.Count);
        Assert.Equal("Marca", tabla.Columnas[0]);
        Assert.Equal("Nombre", tabla.Columnas[1]);
        Assert.Equal("Fecha", tabla.Columnas[2]);

        Assert.Single(tabla.Filas);
        Assert.Equal(2, tabla.Filas[0].Numero);
        Assert.Equal("AB", tabla.Filas[0].Valores[0]);
        Assert.Equal("PEREZ; JUAN", tabla.Filas[0].Valores[1]);
        Assert.Equal("01/02/2020", tabla.Filas[0].Valores[2]);
    }

    [Fact]
    public void Leer_DelimitadorComa_DetectaYLeeCorrectamente()
    {
        var csv = "Marca,Nombre,Fecha\nAB,PEREZ,01/02/2020\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var tabla = _lector.Leer(datos, null, "HUELLA2");

        Assert.Equal(3, tabla.Columnas.Count);
        Assert.Single(tabla.Filas);
        Assert.Equal("AB", tabla.Filas[0].Valores[0]);
        Assert.Equal("PEREZ", tabla.Filas[0].Valores[1]);
    }

    [Fact]
    public void Leer_Utf8ConBom_PrimeraColumnaSinBomCincoCaracteres()
    {
        var csv = "Marca;Nombre\nAB;JUAN\n";
        var bytes = Encoding.UTF8.GetBytes(csv);
        var datos = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(bytes).ToArray();

        var tabla = _lector.Leer(datos, null, "HUELLA3");

        Assert.Equal("Marca", tabla.Columnas[0]);
        Assert.Equal(5, tabla.Columnas[0].Length);
    }

    [Fact]
    public void Leer_CodificacionDetectada_IdentificaWindows1252YUtf8SinBom()
    {
        // 1. Windows-1252 con "PEÑA"
        var cod1252 = Encoding.GetEncoding(1252);
        var datos1252 = cod1252.GetBytes("Marca;Nombre\nAB;PEÑA\n");
        var t1252 = _lector.Leer(datos1252, null, "H1");
        Assert.Equal("Windows-1252", t1252.Codificacion);
        Assert.Equal("PEÑA", t1252.Filas[0].Valores[1]);

        // 2. UTF-8 sin BOM con "PEÑA"
        var datosUtf8 = Encoding.UTF8.GetBytes("Marca;Nombre\nAB;PEÑA\n");
        var tUtf8 = _lector.Leer(datosUtf8, null, "H2");
        Assert.Equal("UTF-8", tUtf8.Codificacion);
        Assert.Equal("PEÑA", tUtf8.Filas[0].Valores[1]);
    }

    [Fact]
    public void Leer_FilasIrregulares_TrataCortaLargaYVaciaSegunReglas()
    {
        // Fila 2: corta (solo 1 valor para 3 cols)
        // Fila 3: larga (4 valores para 3 cols)
        // Fila 4: vacía (;; -> 3 valores vacíos)
        // Fila 5: normal
        var csv = "C1;C2;C3\nV1;\nV1;V2;V3;V4\n;;\nN1;N2;N3\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var tabla = _lector.Leer(datos, null, "HUELLA5");

        // Fila corta
        Assert.Equal(2, tabla.Filas[0].Numero);
        Assert.Equal("V1", tabla.Filas[0].Valores[0]);
        Assert.Null(tabla.Filas[0].Valores[1]);
        Assert.Null(tabla.Filas[0].Valores[2]);

        // Fila larga
        Assert.Equal(3, tabla.Filas[1].Numero);
        Assert.Equal("V1", tabla.Filas[1].Valores[0]);
        Assert.Equal("V2", tabla.Filas[1].Valores[1]);
        Assert.Equal("V3", tabla.Filas[1].Valores[2]);
        Assert.Equal(3, tabla.Filas[1].Valores.Count);

        // Fila normal
        Assert.Equal(5, tabla.Filas[2].Numero);

        // Advertencias
        Assert.Contains(tabla.Advertencias, a => a.Contains("más valores que columnas") && a.Contains("fila 3"));
        Assert.Contains(tabla.Advertencias, a => a.Contains("1 fila(s) vacía(s)"));
    }

    [Fact]
    public void Leer_ComillaSinCerrar_LanzaValidacionExceptionConFilaYComillas()
    {
        var csv = "ID;Texto;Otro\n1;\"sin cerrar\n2;3\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var ex = Assert.Throws<ValidacionException>(() => _lector.Leer(datos, null, "H"));
        Assert.Contains("fila 2", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("comillas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leer_VacioYSoloCabecera_ComportamientoEsperado()
    {
        // 1. Vacío
        var ex = Assert.Throws<ValidacionException>(() => _lector.Leer(Array.Empty<byte>(), null, "H"));
        Assert.Contains("El archivo está vacío.", ex.Message);

        // 2. Solo cabecera
        var datosSoloCabecera = Encoding.UTF8.GetBytes("Col1;Col2\n");
        var tabla = _lector.Leer(datosSoloCabecera, null, "H");
        Assert.Equal(2, tabla.Columnas.Count);
        Assert.Empty(tabla.Filas);
    }

    [Fact]
    public void Leer_CabeceraConRepetidasYVacia_NormalizaColumnasYAgregaAdvertencia()
    {
        var csv = "A;A;\n1;2;3\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var tabla = _lector.Leer(datos, null, "H");

        Assert.Equal(3, tabla.Columnas.Count);
        Assert.Equal("A", tabla.Columnas[0]);
        Assert.Equal("A_2", tabla.Columnas[1]);
        Assert.Equal("COLUMNA3", tabla.Columnas[2]);
        Assert.Contains(tabla.Advertencias, a => a.Contains("«A»") && a.Contains("«A_2»"));
    }

    [Fact]
    public void Leer_ValoresConEspaciosYVacios_RecortaYConvierteEnNull()
    {
        var csv = "C1;C2\n  x  ;\"\"\n";
        var datos = Encoding.UTF8.GetBytes(csv);

        var tabla = _lector.Leer(datos, null, "H");

        Assert.Single(tabla.Filas);
        Assert.Equal("x", tabla.Filas[0].Valores[0]);
        Assert.Null(tabla.Filas[0].Valores[1]);
    }
}
