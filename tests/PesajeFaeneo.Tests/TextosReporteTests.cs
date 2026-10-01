using System.Globalization;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class TextosReporteTests
{
    private static FilaReporte CrearFila(long boleta = 32768, OrigenPeso origen = OrigenPeso.Balanza)
    {
        return new FilaReporte(
            Item: 1,
            PesajeId: 1,
            NumeroBoleta: boleta,
            FechaHoraLocal: new DateTime(2026, 10, 1, 14, 30, 0, DateTimeKind.Unspecified),
            Secuencia: 1,
            MarcaGanadero: "CAB",
            NombreGanadero: "GANADERO",
            CodigoCliente: 101,
            NombreCliente: "CLIENTE",
            Direccion: "CALLE 123",
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 100m,
            PesoDerecho: 100m,
            PesoTotal: 200m,
            Origen: origen,
            AnuladoEnLocal: null,
            AnuladoPor: null,
            MotivoAnulacion: null);
    }

    private static ReportePesajes CrearReporte(
        TipoReporte tipo,
        DateOnly desde,
        DateOnly hasta,
        string? clave = null)
    {
        return new ReportePesajes(
            Tipo: tipo,
            Desde: desde,
            Hasta: hasta,
            ClaveFiltro: clave,
            Empresa: "EMPRESA PRUEBA",
            RutaLogo: null,
            Titulo: "TITULO",
            Subtitulo: "SUBTITULO",
            GeneradoPor: "admin",
            GeneradoEnLocal: new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Unspecified),
            Filas: Array.Empty<FilaReporte>(),
            Totales: new TotalesReporte(0, 0, 0, 0m, 0m, 0m, 0m, 0));
    }

    [Theory]
    [InlineData(TipoReporte.PesajesDelDia, "Pesajes del día")]
    [InlineData(TipoReporte.PesajesEntreFechas, "Pesajes entre fechas")]
    [InlineData(TipoReporte.PorGanadero, "Pesajes por ganadero")]
    [InlineData(TipoReporte.PorCliente, "Pesajes por cliente")]
    [InlineData(TipoReporte.Anuladas, "Boletas anuladas")]
    public void NombreTipo_ValoresValidos_RetornaTextoEsperado(TipoReporte tipo, string esperado)
    {
        Assert.Equal(esperado, TextosReporte.NombreTipo(tipo));
    }

    [Fact]
    public void NombreTipo_ValorDesconocido_RetornaToString()
    {
        Assert.Equal("99", TextosReporte.NombreTipo((TipoReporte)99));
    }

    [Theory]
    [InlineData(TipoReporte.PesajesDelDia, "Reporte de Pesajes del Día")]
    [InlineData(TipoReporte.PesajesEntreFechas, "Reporte de Pesajes Fecha a Fecha")]
    [InlineData(TipoReporte.PorGanadero, "Reporte por Ganadero")]
    [InlineData(TipoReporte.PorCliente, "Reporte por Cliente Fecha a Fecha")]
    [InlineData(TipoReporte.Anuladas, "Reporte de Boletas Anuladas")]
    public void Titulo_ValoresValidos_RetornaTextoEsperado(TipoReporte tipo, string esperado)
    {
        Assert.Equal(esperado, TextosReporte.Titulo(tipo));
    }

    [Fact]
    public void Titulo_ValorInvalido_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextosReporte.Titulo((TipoReporte)99));
    }

    [Fact]
    public void Rango_FechasIgualesODistintas_RetornaFormatoCorrecto()
    {
        var d1 = new DateOnly(2018, 8, 4);
        var d2 = new DateOnly(2018, 9, 10);

        Assert.Equal("Del día: 04/08/2018", TextosReporte.Rango(d1, d1));
        Assert.Equal("De fecha: 04/08/2018 a 10/09/2018", TextosReporte.Rango(d1, d2));
    }

    [Fact]
    public void Subtitulo_CincoTipos_RetornaTextoEsperado()
    {
        var d1 = new DateOnly(2026, 10, 1);
        var d2 = new DateOnly(2026, 10, 5);

        Assert.Equal("Del día: 01/10/2026", TextosReporte.Subtitulo(TipoReporte.PesajesDelDia, d1, d1, null, null));
        Assert.Equal("De fecha: 01/10/2026 a 05/10/2026", TextosReporte.Subtitulo(TipoReporte.PesajesEntreFechas, d1, d2, null, null));
        Assert.Equal("De fecha: 01/10/2026 a 05/10/2026 — Ganadero: CAB", TextosReporte.Subtitulo(TipoReporte.PorGanadero, d1, d2, "CAB", null));
        Assert.Equal("De fecha: 01/10/2026 a 05/10/2026 — Cliente: 101", TextosReporte.Subtitulo(TipoReporte.PorCliente, d1, d2, null, "101"));
        Assert.Equal("De fecha: 01/10/2026 a 05/10/2026 (fecha de emisión de la boleta)", TextosReporte.Subtitulo(TipoReporte.Anuladas, d1, d2, null, null));
    }

    [Fact]
    public void Kg_Reses_Entero_FormateaSegunCulturaEsBO()
    {
        Assert.Equal("2.038,0", TextosReporte.Kg(2038.0m));
        Assert.Equal("0,0", TextosReporte.Kg(0m));
        Assert.Equal("8", TextosReporte.Reses(8m));
        Assert.Equal("9,5", TextosReporte.Reses(9.5m));
        Assert.Equal("1.000", TextosReporte.Entero(1000));
    }

    [Fact]
    public void NumeroBoleta_SinSeparadorMiles_MarcaSimuladoConAsterisco()
    {
        var filaNormal = CrearFila(boleta: 32768, origen: OrigenPeso.Balanza);
        var filaSimulada = CrearFila(boleta: 32768, origen: OrigenPeso.Simulador);

        Assert.Equal("32768", TextosReporte.NumeroBoleta(filaNormal));
        Assert.Equal("32768*", TextosReporte.NumeroBoleta(filaSimulada));
    }

    [Fact]
    public void FechaYFechaHora_DeleganEnTextosBoleta()
    {
        var fecha = new DateTime(2026, 10, 1, 14, 5, 0, DateTimeKind.Unspecified);
        Assert.Equal("01/10/2026", TextosReporte.Fecha(fecha));
        Assert.Equal("01/10/2026 14:05", TextosReporte.FechaHora(fecha));
    }

    [Theory]
    [InlineData(OrigenPeso.Balanza, "BALANZA")]
    [InlineData(OrigenPeso.Simulador, "SIMULADO")]
    [InlineData(OrigenPeso.Importado, "IMPORTADA")]
    public void Origen_RetornaEtiquetaMayuscula(OrigenPeso origen, string esperado)
    {
        Assert.Equal(esperado, TextosReporte.Origen(origen));
    }

    [Fact]
    public void Generado_RetornaEncabezadoCompleto()
    {
        var dt = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal("Generado el 01/10/2026 09:30 por Juan Perez", TextosReporte.Generado("Juan Perez", dt));
    }

    [Fact]
    public void Totales_FormatosDeResumen()
    {
        var totales = new TotalesReporte(11, 8, 3, 9.5m, 1190m, 848m, 2038m, 1);

        Assert.Equal("Total Peso Neto: 2.038,0 Kg.", TextosReporte.TotalPesoNeto(totales));
        Assert.Equal("Cantidad Reses: 9,5", TextosReporte.CantidadReses(totales));
        Assert.Equal("Boletas: 11 (res completa: 8 · media res: 3)", TextosReporte.DetalleBoletas(totales));
        Assert.Equal("Boletas anuladas: 11", TextosReporte.BoletasAnuladas(totales));
        Assert.Equal("Peso neto anulado: 2.038,0 Kg.", TextosReporte.PesoNetoAnulado(totales));
    }

    [Fact]
    public void NotaSimuladas_CeroEsNulo_MayorQueCeroRetornaMensaje()
    {
        Assert.Null(TextosReporte.NotaSimuladas(0));
        Assert.Equal("(*) 3 boleta(s) con peso SIMULADO — sin valor comercial.", TextosReporte.NotaSimuladas(3));
    }

    [Fact]
    public void Constantes_TextosExactos()
    {
        Assert.Equal("SISTEMA DE PESAJE PARA FAENEO", TextosReporte.EmpresaPorDefecto);
        Assert.Equal("No hay boletas para los filtros seleccionados.", TextosReporte.SinBoletas);
        Assert.Equal("El reporte supera el máximo de 20.000 boletas. Reduzca el rango de fechas.", TextosReporte.MaximoSuperado);
        Assert.Contains(ReglasReportes.MaximoFilas.ToString("#,##0", TextosBoleta.Cultura), TextosReporte.MaximoSuperado);
    }

    [Fact]
    public void NombreHoja_RetornaPesajesOAnuladas()
    {
        Assert.Equal("Pesajes", TextosReporte.NombreHoja(false));
        Assert.Equal("Anuladas", TextosReporte.NombreHoja(true));
    }

    [Fact]
    public void NombreArchivo_CincoTiposConYSinRango_SaneaCaracteresInvalidos()
    {
        var d1 = new DateOnly(2026, 10, 1);
        var d2 = new DateOnly(2026, 10, 5);

        var rDia = CrearReporte(TipoReporte.PesajesDelDia, d1, d1);
        Assert.Equal("Pesajes_del_dia_2026-10-01.pdf", TextosReporte.NombreArchivo(rDia, ".pdf"));

        var rFechas = CrearReporte(TipoReporte.PesajesEntreFechas, d1, d2);
        Assert.Equal("Pesajes_2026-10-01_a_2026-10-05.xlsx", TextosReporte.NombreArchivo(rFechas, "xlsx"));

        // R20: Marca "A/B" no debe contener '/' en el nombre de archivo
        var rGanadero = CrearReporte(TipoReporte.PorGanadero, d1, d1, clave: "A/B");
        Assert.Equal("Ganadero_A_B_2026-10-01.pdf", TextosReporte.NombreArchivo(rGanadero, ".pdf"));

        var rCliente = CrearReporte(TipoReporte.PorCliente, d1, d2, clave: "105");
        Assert.Equal("Cliente_105_2026-10-01_a_2026-10-05.xlsx", TextosReporte.NombreArchivo(rCliente, ".xlsx"));

        var rAnuladas = CrearReporte(TipoReporte.Anuladas, d1, d2);
        Assert.Equal("Anuladas_2026-10-01_a_2026-10-05.pdf", TextosReporte.NombreArchivo(rAnuladas, ".pdf"));
    }

    [Fact]
    public void IndependenciaDeCultura_ConEnUs_NoModificaResultados()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            Assert.Equal("2.038,0", TextosReporte.Kg(2038.0m));
            Assert.Equal("0,0", TextosReporte.Kg(0m));
            Assert.Equal("9,5", TextosReporte.Reses(9.5m));
            Assert.Equal("Del día: 04/08/2018", TextosReporte.Rango(new DateOnly(2018, 8, 4), new DateOnly(2018, 8, 4)));
            // Entero detecta CurrentCulture (en-US usaría coma de miles); las fechas son dd/MM/yyyy en ambas culturas,
            // así que su aserción solo fija el formato esperado.
            Assert.Equal("1.234.567", TextosReporte.Entero(1234567));
            Assert.Equal("04/08/2018", TextosReporte.Fecha(new DateTime(2018, 8, 4, 15, 30, 0)));
            Assert.Equal("04/08/2018 15:30", TextosReporte.FechaHora(new DateTime(2018, 8, 4, 15, 30, 0)));
            Assert.Equal("Generado el 04/08/2018 15:30 por Ana", TextosReporte.Generado("Ana", new DateTime(2018, 8, 4, 15, 30, 0)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void NombreArchivo_PorGanaderoYDelDia_ConYSinRango()
    {
        var d1 = new DateOnly(2026, 10, 1);
        var d2 = new DateOnly(2026, 10, 5);

        // Con rango (Hasta != Desde): sufijo "_a_" también en el reporte por ganadero.
        var rGanaderoRango = CrearReporte(TipoReporte.PorGanadero, d1, d2, clave: "G1");
        Assert.Equal("Ganadero_G1_2026-10-01_a_2026-10-05.pdf", TextosReporte.NombreArchivo(rGanaderoRango, ".pdf"));

        // Sin rango en reportes que normalmente lo llevan.
        var rFechasUnDia = CrearReporte(TipoReporte.PesajesEntreFechas, d1, d1);
        Assert.Equal("Pesajes_2026-10-01.pdf", TextosReporte.NombreArchivo(rFechasUnDia, ".pdf"));
        var rClienteUnDia = CrearReporte(TipoReporte.PorCliente, d1, d1, clave: "105");
        Assert.Equal("Cliente_105_2026-10-01.xlsx", TextosReporte.NombreArchivo(rClienteUnDia, ".xlsx"));
        var rAnuladasUnDia = CrearReporte(TipoReporte.Anuladas, d1, d1);
        Assert.Equal("Anuladas_2026-10-01.pdf", TextosReporte.NombreArchivo(rAnuladasUnDia, ".pdf"));
    }
}
