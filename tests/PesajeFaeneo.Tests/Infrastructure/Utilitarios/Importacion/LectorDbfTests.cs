using System.Text;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using PesajeFaeneo.Tests.Utilidades.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class LectorDbfTests
{
    [Fact]
    public void Leer_Ganaderos850_DevuelveEstructuraYFilasCorrectasConEliminados()
    {
        using var ms = new MemoryStream(ArchivosDbfDePrueba.Ganaderos850);
        using var lector = new LectorDbf(ms, null);

        Assert.Equal(850, lector.PaginaDeCodigos);
        Assert.Equal(5, lector.Columnas.Count);
        Assert.Equal("MARCA", lector.Columnas[0].Nombre);
        Assert.Equal('C', lector.Columnas[0].Tipo);
        Assert.Equal(5, lector.Columnas[0].Longitud);

        Assert.Equal("NOMBRE", lector.Columnas[1].Nombre);
        Assert.Equal('C', lector.Columnas[1].Tipo);
        Assert.Equal(40, lector.Columnas[1].Longitud);

        Assert.Equal("PESO", lector.Columnas[2].Nombre);
        Assert.Equal('N', lector.Columnas[2].Tipo);
        Assert.Equal(8, lector.Columnas[2].Longitud);

        Assert.Equal("FECHA", lector.Columnas[3].Nombre);
        Assert.Equal('D', lector.Columnas[3].Tipo);
        Assert.Equal(8, lector.Columnas[3].Longitud);

        Assert.Equal("ACTIVO", lector.Columnas[4].Nombre);
        Assert.Equal('L', lector.Columnas[4].Tipo);
        Assert.Equal(1, lector.Columnas[4].Longitud);

        // Fila 1
        var f1 = lector.LeerSiguiente();
        Assert.NotNull(f1);
        Assert.Equal(1, lector.NumeroRegistroActual);
        Assert.Equal("AB", f1[0]);
        Assert.Equal("PEÑA ÁLVAREZ", f1[1]);
        Assert.Equal("123.5", f1[2]);
        Assert.Equal("2020-02-01", f1[3]);
        Assert.Equal("true", f1[4]);

        // Fila 2
        var f2 = lector.LeerSiguiente();
        Assert.NotNull(f2);
        Assert.Equal(2, lector.NumeroRegistroActual);
        Assert.Equal("CD", f2[0]);
        Assert.Equal("GOMEZ", f2[1]);
        Assert.Null(f2[2]);
        Assert.Null(f2[3]);
        Assert.Equal("false", f2[4]);

        // Fila 3 (el registro 3 estaba borrado: *EF...)
        var f3 = lector.LeerSiguiente();
        Assert.NotNull(f3);
        Assert.Equal(4, lector.NumeroRegistroActual);
        Assert.Equal("GH", f3[0]);
        Assert.Equal("ÚLTIMO", f3[1]);
        Assert.Equal("-5.2", f3[2]);
        Assert.Equal("2019-12-31", f3[3]);
        Assert.Null(f3[4]);

        // Fin
        Assert.Null(lector.LeerSiguiente());
        Assert.Equal(1, lector.Eliminados);
    }

    [Fact]
    public void Leer_PesajesVfp1252_DevuelveEstructuraYFilasVfpCorrectas()
    {
        using var ms = new MemoryStream(ArchivosDbfDePrueba.PesajesVfp1252);
        using var lector = new LectorDbf(ms, null);

        Assert.Equal(1252, lector.PaginaDeCodigos);
        Assert.Equal(6, lector.Columnas.Count);

        Assert.Equal("NUMERO", lector.Columnas[0].Nombre);
        Assert.Equal('I', lector.Columnas[0].Tipo);
        Assert.True(lector.Columnas[0].Soportada);

        Assert.Equal("IMPORTE", lector.Columnas[1].Nombre);
        Assert.Equal('Y', lector.Columnas[1].Tipo);
        Assert.True(lector.Columnas[1].Soportada);

        Assert.Equal("FECHAHORA", lector.Columnas[2].Nombre);
        Assert.Equal('T', lector.Columnas[2].Tipo);
        Assert.True(lector.Columnas[2].Soportada);

        Assert.Equal("PESO", lector.Columnas[3].Nombre);
        Assert.Equal('B', lector.Columnas[3].Tipo);
        Assert.True(lector.Columnas[3].Soportada);

        Assert.Equal("NOTA", lector.Columnas[4].Nombre);
        Assert.Equal('M', lector.Columnas[4].Tipo);
        Assert.False(lector.Columnas[4].Soportada);

        Assert.Equal("NOMBRE", lector.Columnas[5].Nombre);
        Assert.Equal('C', lector.Columnas[5].Tipo);
        Assert.True(lector.Columnas[5].Soportada);

        // Fila 1
        var f1 = lector.LeerSiguiente();
        Assert.NotNull(f1);
        Assert.Equal("1001", f1[0]);
        Assert.Equal("12.3456", f1[1]);
        Assert.Equal("2020-05-06 07:08:09", f1[2]);
        Assert.Equal("250.75", f1[3]);
        Assert.Null(f1[4]);
        Assert.Equal("NIÑO", f1[5]);

        // Fila 2
        var f2 = lector.LeerSiguiente();
        Assert.NotNull(f2);
        Assert.Equal("1002", f2[0]);
        Assert.Equal("0", f2[1]);
        Assert.Null(f2[2]);
        Assert.Equal("0", f2[3]);
        Assert.Null(f2[4]);
        Assert.Null(f2[5]);

        Assert.Null(lector.LeerSiguiente());
    }

    [Fact]
    public void Leer_Ganaderos850Forzando1252_DecodificaConCaracteresDiferentes()
    {
        using var ms = new MemoryStream(ArchivosDbfDePrueba.Ganaderos850);
        using var lector = new LectorDbf(ms, 1252);

        var f1 = lector.LeerSiguiente();
        Assert.NotNull(f1);
        Assert.Equal("PE¥A µLVAREZ", f1[1]);
    }

    [Fact]
    public void Constructor_TextoPlano_LanzaInvalidDataExceptionConMensajeEsperado()
    {
        var bytes = Encoding.UTF8.GetBytes("Esto no es un archivo DBF válido de dBase o FoxPro");
        using var ms = new MemoryStream(bytes);

        var ex = Assert.Throws<InvalidDataException>(() => new LectorDbf(ms, null));
        Assert.Contains("no es una tabla dBase/FoxPro reconocida", ex.Message);
    }

    [Fact]
    public void Leer_ArchivoTruncado_LanzaInvalidDataExceptionRegistroIncompleto()
    {
        var truncado = ArchivosDbfDePrueba.Ganaderos850[..^30];
        using var ms = new MemoryStream(truncado);
        using var lector = new LectorDbf(ms, null);

        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            while (lector.LeerSiguiente() != null)
            {
            }
        });

        Assert.Contains("el registro 4 está incompleto", ex.Message);
    }

    [Fact]
    public void EscritorDbfDePrueba_CasosLimite_SeComportaSegunEspecificacion()
    {
        var enc = Encoding.GetEncoding(1252);

        // 1. Tabla sin registros
        var campos = new List<(string Nombre, char Tipo, int Longitud, int Decimales)>
        {
            ("COL1", 'C', 10, 0)
        };
        var bytesVacio = EscritorDbfDePrueba.Crear(0x03, campos, Array.Empty<(bool, string)>(), enc);
        using (var msVacio = new MemoryStream(bytesVacio))
        using (var lectorVacio = new LectorDbf(msVacio, null))
        {
            Assert.Single(lectorVacio.Columnas);
            Assert.Null(lectorVacio.LeerSiguiente());
        }

        // 2. Casos N="*****", D="00000000", L="?", C de 300 caracteres, y fin de archivo prematuro con 0x1A
        var camposEspeciales = new List<(string Nombre, char Tipo, int Longitud, int Decimales)>
        {
            ("NUM", 'N', 5, 0),
            ("FEC", 'D', 8, 0),
            ("LOG", 'L', 1, 0),
            ("LARGO", 'C', 300, 0)
        };

        var texto300 = new string('X', 300);
        var crudo = "*****" + "00000000" + "?" + texto300;
        var registros = new List<(bool Borrado, string Crudo)>
        {
            (false, crudo)
        };

        // Declaramos 10 registros pero solo escribimos 1, con 0x1A al final
        var bytesEsp = EscritorDbfDePrueba.Crear(0x03, camposEspeciales, registros, enc, cantidadDeclarada: 10);
        using var msEsp = new MemoryStream(bytesEsp);
        using var lectorEsp = new LectorDbf(msEsp, null);

        var fila = lectorEsp.LeerSiguiente();
        Assert.NotNull(fila);
        Assert.Null(fila[0]); // N="*****" -> null
        Assert.Null(fila[1]); // D="00000000" -> null
        Assert.Null(fila[2]); // L="?" -> null
        Assert.Equal(texto300, fila[3]); // C de 300 chars se lee completo

        // Siguiente debe ser null porque encuentra 0x1A sin error
        Assert.Null(lectorEsp.LeerSiguiente());
    }
}
