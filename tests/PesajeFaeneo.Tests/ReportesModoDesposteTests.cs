using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Reportes;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D6, D9): reportes sin columna "Tipo Corte" ni desglose por tipo cuando la empresa pesa un solo tipo.</summary>
public class ReportesModoDesposteTests
{
    // ---- Servicio ----

    private static (ServicioReportes Servicio, SesionActual Sesion) CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("F11", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));
        var sesion = new SesionActual();
        return (new ServicioReportes(db.UnidadDeTrabajoFactory, sesion, tiempo, new AlmacenLogoFalso(null), NullLogger<ServicioReportes>.Instance), sesion);
    }

    private static async Task SembrarAsync(BaseDatosEnMemoria db, ModoDesposte? modo, params (TipoDesposte Tipo, bool Anulado)[] pesajes)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = new GanaderoBuilder().ConCodigo("7").Build();
        var c = new ClienteBuilder().ConCodigo(101).Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_f11").Build();
        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        (await ctx.ParametrosSistema.SingleAsync()).ModoDesposte = modo;
        await ctx.SaveChangesAsync();

        var numero = 1;
        foreach (var (tipo, anulado) in pesajes)
        {
            var b = new PesajeBuilder()
                .ConNumeroBoleta(numero)
                .ConFechaHora(new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc).AddMinutes(numero))
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(u.Id)
                .ConDesposte(tipo)
                .ConPesos(110m, tipo == TipoDesposte.ResCompleta ? 108m : 0m);
            if (anulado)
            {
                b = b.Anulado("ERROR DE PESAJE", u.Id);
            }

            ctx.Pesajes.Add(b.Build());
            numero++;
        }

        await ctx.SaveChangesAsync();
    }

    private static readonly DateOnly Dia = new(2026, 10, 8);

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta, false)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes, false)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.ResCompleta, true)]
    [InlineData(null, TipoDesposte.ResCompleta, true)]
    public async Task Generar_TodasLasFilasDelTipoConfigurado(ModoDesposte? modo, TipoDesposte tipo, bool esperado)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        await SembrarAsync(db, modo, (tipo, false), (tipo, false));

        var reporte = await servicio.GenerarAsync(new FiltroReporte(TipoReporte.PesajesDelDia, Dia, Dia, null, null));

        Assert.Equal(2, reporte.Filas.Count);
        Assert.Equal(esperado, reporte.MostrarDesposte);
    }

    [Fact]
    public async Task Generar_ConUnaBoletaHistoricaDeOtroTipo_MuestraElDesposte()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        await SembrarAsync(db, ModoDesposte.SoloResCompleta, (TipoDesposte.ResCompleta, false), (TipoDesposte.MediaRes, false));

        var reporte = await servicio.GenerarAsync(new FiltroReporte(TipoReporte.PesajesDelDia, Dia, Dia, null, null));

        Assert.True(reporte.MostrarDesposte);
        Assert.Equal(1.5m, reporte.Totales.Reses);
    }

    [Fact]
    public async Task Generar_Anuladas_EvaluaSoloLasFilasDelReporte()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        // La media res vigente no entra al reporte de anuladas: no obliga a mostrar el desposte.
        await SembrarAsync(db, ModoDesposte.SoloResCompleta, (TipoDesposte.ResCompleta, true), (TipoDesposte.MediaRes, false));

        var reporte = await servicio.GenerarAsync(new FiltroReporte(TipoReporte.Anuladas, Dia, Dia, null, null));

        Assert.Single(reporte.Filas);
        Assert.False(reporte.MostrarDesposte);
    }

    [Fact]
    public async Task Generar_ReporteVacioConModoUnico_SinDesposte()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        await SembrarAsync(db, ModoDesposte.SoloResCompleta);

        var reporte = await servicio.GenerarAsync(new FiltroReporte(TipoReporte.PesajesDelDia, Dia, Dia, null, null));

        Assert.Empty(reporte.Filas);
        Assert.False(reporte.MostrarDesposte);
    }

    // ---- Columnas y textos ----

    [Theory]
    [InlineData(false, 12, 11)]
    [InlineData(true, 10, 9)]
    public void Columnas_SinDesposte_OmiteTipoCorte(bool anuladas, int con, int sin)
    {
        var completas = ColumnasReporte.Para(anuladas);
        var reducidas = ColumnasReporte.Para(anuladas, mostrarDesposte: false);

        Assert.Equal(con, completas.Count);
        Assert.Equal(sin, reducidas.Count);
        Assert.Contains(completas, c => c.Clave == "Desposte");
        Assert.DoesNotContain(reducidas, c => c.Clave == "Desposte");
        Assert.Equal(completas.Where(c => c.Clave != "Desposte"), reducidas);
        Assert.Same(completas, ColumnasReporte.Para(anuladas, mostrarDesposte: true));
    }

    [Theory]
    [InlineData(TipoReporte.PesajesEntreFechas)]
    [InlineData(TipoReporte.Anuladas)]
    public void Textos_SinDesposte_SeAlineanConLasColumnas(TipoReporte tipo)
    {
        var reporte = DatosReporteDePrueba.Crear(tipo, 3);
        var anuladas = reporte.EsAnuladas;
        var columnas = ColumnasReporte.Para(anuladas, mostrarDesposte: false);

        foreach (var fila in reporte.Filas)
        {
            var completos = ColumnasReporte.Textos(fila, anuladas);
            var textos = ColumnasReporte.Textos(fila, anuladas, mostrarDesposte: false);

            Assert.Equal(columnas.Count, textos.Count);
            Assert.DoesNotContain("RES COMPLETA", textos);
            Assert.DoesNotContain("MEDIA RES", textos);
            var idxTotal = columnas.ToList().FindIndex(c => c.Clave == "Total");
            Assert.Equal(TextosReporte.Kg(fila.PesoTotal), textos[idxTotal]);
            Assert.Equal(completos.Count - 1, textos.Count);
        }
    }

    [Fact]
    public void DetalleBoletas_SinDesposte_NoDesglosaPorTipo()
    {
        var t = new TotalesReporte(5, 4, 1, 4.5m, 0m, 0m, 0m, 0);

        Assert.Equal("Boletas: 5 (res completa: 4 · media res: 1)", TextosReporte.DetalleBoletas(t));
        Assert.Equal("Boletas: 5", TextosReporte.DetalleBoletas(t, mostrarDesposte: false));
    }

    // ---- Excel ----

    private static XLWorkbook GenerarExcel(ReportePesajes reporte)
    {
        var stream = new MemoryStream();
        new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance).GenerarExcelAsync(reporte, stream).GetAwaiter().GetResult();
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    private static bool ContieneTexto(IXLWorksheet ws, string texto) =>
        ws.CellsUsed().Any(c => c.GetString().Contains(texto, StringComparison.Ordinal));

    [Fact]
    public void Excel_Normal_SinDesposte_OrigenSeCorreYLosTotalesSiguenEnElTotalNeto()
    {
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3) with { MostrarDesposte = false };
        using var wb = GenerarExcel(reporte);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal("Total neto (kg)", ws.Cell(6, 13).GetString());
        Assert.Equal("Origen", ws.Cell(6, 14).GetString());
        Assert.True(ws.Cell(6, 15).IsEmpty());
        Assert.False(ContieneTexto(ws, "Tipo corte"));
        Assert.False(ContieneTexto(ws, "RES COMPLETA"));
        Assert.False(ContieneTexto(ws, "media res"));

        Assert.Equal("BALANZA", ws.Cell(7, 14).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(7, 13).DataType);
        Assert.Equal("#,##0.0", ws.Cell(7, 13).Style.NumberFormat.Format);
        Assert.Equal(XLDataType.DateTime, ws.Cell(7, 3).DataType);
        Assert.Equal("dd/MM/yyyy HH:mm", ws.Cell(7, 3).Style.DateFormat.Format);

        var u = 6 + reporte.Filas.Count;
        Assert.Equal("Total peso neto (kg)", ws.Cell(u + 2, 1).GetString());
        Assert.Equal(reporte.Totales.TotalNetoKg, ws.Cell(u + 2, 13).GetValue<decimal>());
        Assert.Equal("Boletas", ws.Cell(u + 4, 1).GetString());
        Assert.True(ws.Cell(u + 5, 1).IsEmpty());
    }

    [Fact]
    public void Excel_Normal_ConDesposte_ConservaElFormatoAnterior()
    {
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3);
        using var wb = GenerarExcel(reporte);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal("Tipo corte", ws.Cell(6, 14).GetString());
        Assert.Equal("Origen", ws.Cell(6, 15).GetString());
        Assert.Equal("RES COMPLETA", ws.Cell(7, 14).GetString());
        var u = 6 + reporte.Filas.Count;
        Assert.Equal("Res completa / media res", ws.Cell(u + 5, 1).GetString());
        Assert.Equal("2 / 1", ws.Cell(u + 5, 13).GetString());
    }

    [Fact]
    public void Excel_Anuladas_SinDesposte_TotalNetoSeCorreYLosTotalesLoSiguen()
    {
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 2) with { MostrarDesposte = false };
        using var wb = GenerarExcel(reporte);
        var ws = wb.Worksheets.First();

        Assert.Equal("Nombre del cliente", ws.Cell(6, 7).GetString());
        Assert.Equal("Total neto (kg)", ws.Cell(6, 8).GetString());
        Assert.Equal("Anulada el", ws.Cell(6, 9).GetString());
        Assert.Equal("Origen", ws.Cell(6, 12).GetString());
        Assert.True(ws.Cell(6, 13).IsEmpty());
        Assert.False(ContieneTexto(ws, "Tipo corte"));

        Assert.Equal("#,##0.0", ws.Cell(7, 8).Style.NumberFormat.Format);
        Assert.Equal(XLDataType.DateTime, ws.Cell(7, 9).DataType);

        var u = 6 + reporte.Filas.Count;
        Assert.Equal("Boletas anuladas", ws.Cell(u + 2, 1).GetString());
        Assert.Equal(2, ws.Cell(u + 2, 8).GetValue<int>());
        Assert.Equal(reporte.Totales.TotalNetoKg, ws.Cell(u + 3, 8).GetValue<decimal>());
    }

    // ---- PDF ----

    [Theory]
    [InlineData(TipoReporte.PesajesEntreFechas)]
    [InlineData(TipoReporte.Anuladas)]
    public async Task Pdf_SinDesposte_GeneraUnPdfValido(TipoReporte tipo)
    {
        var reporte = DatosReporteDePrueba.Crear(tipo, 5) with { MostrarDesposte = false };
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        stream.Position = 0;
        var cabecera = new byte[5];
        Assert.Equal(5, stream.Read(cabecera, 0, 5));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(cabecera));
    }
}
