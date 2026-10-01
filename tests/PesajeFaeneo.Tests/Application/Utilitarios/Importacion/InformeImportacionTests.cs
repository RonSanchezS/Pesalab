using System.Text;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using Xunit;

namespace PesajeFaeneo.Tests.Application.Utilitarios.Importacion;

public class InformeImportacionTests
{
    [Fact]
    public void GenerarCsv_EstructuraYEscapado_CumpleEspecificacionExacta()
    {
        var incidencias = new List<Incidencia>
        {
            new(2, "Campo1", NivelIncidencia.Error, "Error con ; y \"comillas\""),
            new(3, "Campo2", NivelIncidencia.Advertencia, "=SUMA(A1:A2)")
        };

        var resultado = new ResultadoImportacion(
            EsSimulacro: true,
            Huella: "HUELLA123",
            Tipo: TipoImportacion.Ganaderos,
            FilasLeidas: 10,
            FilasAImportar: 8,
            Insertadas: 0,
            FilasOmitidas: 1,
            FilasConError: 1,
            FilasConAdvertencia: 1,
            Incidencias: incidencias,
            IncidenciasTruncadas: true,
            BackupPrevio: null);

        var bytes = InformeImportacion.GenerarCsv(resultado, "origen.csv");

        // 1. Empieza con BOM UTF-8 (EF BB BF)
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var texto = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var lineas = texto.Split(new[] { "\r\n" }, StringSplitOptions.None);

        // 2. 9 líneas de cabecera
        Assert.Equal("Archivo;origen.csv", lineas[0]);
        Assert.Equal("Tipo;Ganaderos", lineas[1]);
        Assert.Equal("Resultado;Simulacro", lineas[2]);
        Assert.Equal("Filas leídas;10", lineas[3]);
        Assert.Equal("A importar;8", lineas[4]);
        Assert.Equal("Insertadas;0", lineas[5]);
        Assert.Equal("Ya existentes (omitidas);1", lineas[6]);
        Assert.Equal("Con errores (no importadas);1", lineas[7]);
        Assert.Equal("Con advertencias;1", lineas[8]);
        Assert.Equal("", lineas[9]); // línea vacía
        Assert.Equal("Fila;Nivel;Campo;Mensaje", lineas[10]);

        // 3. Incidencia con ; y " entre comillas y con " duplicadas
        Assert.Contains("2;Error;Campo1;\"Error con ; y \"\"comillas\"\"\"", texto);

        // 4. Incidencia con = empieza con '=
        Assert.Contains("3;Advertencia;Campo2;'=SUMA(A1:A2)", texto);

        // 5. Línea de truncado aparece porque IncidenciasTruncadas = true
        Assert.Contains("(Se muestran las primeras 2 incidencias)", texto);
    }

    [Fact]
    public void GenerarCsv_SinTruncado_NoMuestraLineaDeTruncado()
    {
        var resultado = new ResultadoImportacion(
            EsSimulacro: false,
            Huella: "HUELLA123",
            Tipo: TipoImportacion.Clientes,
            FilasLeidas: 1,
            FilasAImportar: 1,
            Insertadas: 1,
            FilasOmitidas: 0,
            FilasConError: 0,
            FilasConAdvertencia: 0,
            Incidencias: Array.Empty<Incidencia>(),
            IncidenciasTruncadas: false,
            BackupPrevio: "backup.zip");

        var bytes = InformeImportacion.GenerarCsv(resultado, "clientes.csv");
        var texto = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Contains("Resultado;Importación", texto);
        Assert.DoesNotContain("(Se muestran las primeras", texto);
    }
}
