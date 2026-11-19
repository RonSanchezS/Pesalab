using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ColumnasReporteTests
{
    private static FilaReporte CrearFila(
        int item = 1,
        decimal izq = 106.0m,
        decimal der = 0.0m,
        decimal total = 106.0m,
        TipoDesposte desposte = TipoDesposte.MediaRes,
        DateTime? anuladoEn = null,
        string? anuladoPor = null,
        string? motivo = null,
        string nombreFletero = "FLETERO SIETE")
    {
        return new FilaReporte(
            Item: item,
            PesajeId: 10,
            NumeroBoleta: 5001,
            FechaHoraLocal: new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Unspecified),
            Secuencia: 15,
            CodigoGanadero: "CAB",
            NombreGanadero: "GANADERO CAB",
            CodigoCliente: 202,
            NombreCliente: "CARNICERIA CENTRAL",
            Direccion: "AV. PRINCIPAL #45",
            TipoDesposte: desposte,
            PesoIzquierdo: izq,
            PesoDerecho: der,
            PesoTotal: total,
            Origen: OrigenPeso.Balanza,
            AnuladoEnLocal: anuladoEn,
            AnuladoPor: anuladoPor,
            MotivoAnulacion: motivo,
            NombreFletero: nombreFletero);
    }

    [Fact]
    public void Para_NormalYAnuladas_TienenNumeroYOrdenDeColumnasCorrecto()
    {
        var colNormal = ColumnasReporte.Para(anuladas: false);
        var colAnulada = ColumnasReporte.Para(anuladas: true);

        Assert.Equal(12, colNormal.Count);
        Assert.Equal(10, colAnulada.Count);

        var clavesNormales = colNormal.Select(c => c.Clave).ToArray();
        Assert.Equal(new[] { "Item", "Numero", "Fecha", "Secuencia", "Codigo", "Cliente", "Fletero", "Direccion", "Izquierdo", "Derecho", "Total", "Desposte" }, clavesNormales);

        var clavesAnuladas = colAnulada.Select(c => c.Clave).ToArray();
        Assert.Equal(new[] { "Item", "Numero", "Fecha", "Codigo", "Cliente", "Desposte", "Total", "AnuladaEl", "AnuladaPor", "Motivo" }, clavesAnuladas);
    }

    [Fact]
    public void Para_TitulosAnchosYAlineacion_CoincidenConLaEspecificacion()
    {
        // (Clave, Título, Grupo, AnchoPt, AlineadaDerecha) tal como los fija 3.5 de la especificación.
        var esperadoNormal = new (string, string, string?, float, bool)[]
        {
            ("Item", "Item", null, 24f, false),
            ("Numero", "N° Bol.", null, 40f, false),
            ("Fecha", "Fecha", null, 44f, false),
            ("Secuencia", "N° Res", null, 26f, false),
            ("Codigo", "Código", null, 34f, false),
            ("Cliente", "Cliente", null, 30f, false),
            ("Fletero", "Fletero", null, 64f, false),
            ("Direccion", "Dirección", null, 0f, false),
            ("Izquierdo", "IZQ.", "PESO KG.", 34f, true),
            ("Derecho", "DER.", "PESO KG.", 34f, true),
            ("Total", "TOTAL\nNETO", null, 40f, true),
            ("Desposte", "Tipo Corte", null, 58f, false),
        };
        var esperadoAnuladas = new (string, string, string?, float, bool)[]
        {
            ("Item", "Item", null, 24f, false),
            ("Numero", "N° Bol.", null, 40f, false),
            ("Fecha", "Fecha", null, 44f, false),
            ("Codigo", "Código", null, 34f, false),
            ("Cliente", "Cliente", null, 30f, false),
            ("Desposte", "Tipo Corte", null, 58f, false),
            ("Total", "TOTAL\nNETO", null, 40f, true),
            ("AnuladaEl", "Anulada el", null, 60f, false),
            ("AnuladaPor", "Anulada por", null, 60f, false),
            ("Motivo", "Motivo", null, 0f, false),
        };

        Assert.Equal(
            esperadoNormal,
            ColumnasReporte.Para(anuladas: false).Select(c => (c.Clave, c.Titulo, c.Grupo, c.AnchoPt, c.AlineadaDerecha)).ToArray());
        Assert.Equal(
            esperadoAnuladas,
            ColumnasReporte.Para(anuladas: true).Select(c => (c.Clave, c.Titulo, c.Grupo, c.AnchoPt, c.AlineadaDerecha)).ToArray());
    }

    [Fact]
    public void Para_AmbasListas_TienenExactamenteUnaColumnaRelativa()
    {
        var colNormal = ColumnasReporte.Para(anuladas: false);
        var colAnulada = ColumnasReporte.Para(anuladas: true);

        var relNormal = colNormal.Where(c => c.AnchoPt == 0f).ToList();
        var relAnulada = colAnulada.Where(c => c.AnchoPt == 0f).ToList();

        Assert.Single(relNormal);
        Assert.Equal("Direccion", relNormal[0].Clave);

        Assert.Single(relAnulada);
        Assert.Equal("Motivo", relAnulada[0].Clave);
    }

    [Fact]
    public void Para_SumaDeAnchosFijos_Es428Y390()
    {
        var colNormal = ColumnasReporte.Para(anuladas: false);
        var colAnulada = ColumnasReporte.Para(anuladas: true);

        var sumaNormal = colNormal.Sum(c => c.AnchoPt);
        var sumaAnulada = colAnulada.Sum(c => c.AnchoPt);

        Assert.Equal(428f, sumaNormal);
        Assert.Equal(390f, sumaAnulada);
    }

    [Fact]
    public void Para_GruposDeColumnas_SoloIzquierdoYDerechoPertenecenAPesoKg()
    {
        var colNormal = ColumnasReporte.Para(anuladas: false);
        var colAnulada = ColumnasReporte.Para(anuladas: true);

        var conGrupoNormal = colNormal.Where(c => c.Grupo != null).ToList();
        Assert.Equal(2, conGrupoNormal.Count);
        Assert.All(conGrupoNormal, c => Assert.Equal("PESO KG.", c.Grupo));
        Assert.Contains(conGrupoNormal, c => c.Clave == "Izquierdo");
        Assert.Contains(conGrupoNormal, c => c.Clave == "Derecho");

        Assert.All(colAnulada, c => Assert.Null(c.Grupo));
    }

    [Fact]
    public void Textos_Normal_DevuelveMismoNumeroYOrdenQueColumnas()
    {
        var fila = CrearFila(item: 1, izq: 106.0m, der: 0.0m, total: 106.0m, desposte: TipoDesposte.MediaRes);
        var columnas = ColumnasReporte.Para(anuladas: false);
        var textos = ColumnasReporte.Textos(fila, anuladas: false);

        Assert.Equal(columnas.Count, textos.Count);
        Assert.Equal("1", textos[0]);               // Item
        Assert.Equal("5001", textos[1]);            // Numero
        Assert.Equal("01/10/2026", textos[2]);      // Fecha
        Assert.Equal("15", textos[3]);              // Secuencia
        Assert.Equal("CAB", textos[4]);             // Código del ganadero
        Assert.Equal("202", textos[5]);             // Cliente
        Assert.Equal("FLETERO SIETE", textos[6]);   // Fletero
        Assert.Equal("AV. PRINCIPAL #45", textos[7]);// Direccion
        Assert.Equal("106,0", textos[8]);           // Izquierdo
        Assert.Equal("0,0", textos[9]);             // Derecho
        Assert.Equal("106,0", textos[10]);          // Total
        Assert.Equal("MEDIA RES", textos[11]);      // Desposte
    }

    [Fact]
    public void Textos_AnuladaConFechaYMotivoCompleto()
    {
        string motivo100 = new string('M', 100);
        var fechaAnulacion = new DateTime(2026, 10, 1, 11, 45, 0, DateTimeKind.Unspecified);
        var fila = CrearFila(
            item: 2,
            desposte: TipoDesposte.ResCompleta,
            anuladoEn: fechaAnulacion,
            anuladoPor: "Carlos Sanchez",
            motivo: motivo100);

        var columnas = ColumnasReporte.Para(anuladas: true);
        var textos = ColumnasReporte.Textos(fila, anuladas: true);

        Assert.Equal(columnas.Count, textos.Count);
        Assert.Equal("2", textos[0]);
        Assert.Equal("5001", textos[1]);
        Assert.Equal("01/10/2026", textos[2]);
        Assert.Equal("CAB", textos[3]);
        Assert.Equal("202", textos[4]);
        Assert.Equal("RES COMPLETA", textos[5]);
        Assert.Equal("106,0", textos[6]);
        Assert.Equal("01/10/2026 11:45", textos[7]);
        Assert.Equal("Carlos Sanchez", textos[8]);
        Assert.Equal(motivo100, textos[9]);
    }

    [Fact]
    public void Textos_AnuladaConValoresNulos_RetornaCadenasVacias()
    {
        var fila = CrearFila(item: 3, anuladoEn: null, anuladoPor: null, motivo: null);
        var textos = ColumnasReporte.Textos(fila, anuladas: true);

        Assert.Equal(string.Empty, textos[7]); // Anulada el
        Assert.Equal(string.Empty, textos[8]); // Anulada por
        Assert.Equal(string.Empty, textos[9]); // Motivo
    }

    [Fact]
    public void Textos_FilaNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ColumnasReporte.Textos(null!, anuladas: false));
    }
}
