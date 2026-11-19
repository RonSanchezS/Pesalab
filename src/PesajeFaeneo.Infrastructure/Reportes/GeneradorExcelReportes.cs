using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Infrastructure.Reportes;

public sealed class GeneradorExcelReportes : IGeneradorExcelReportes
{
    private readonly ILogger<GeneradorExcelReportes> _logger;

    public GeneradorExcelReportes(ILogger<GeneradorExcelReportes> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task GenerarExcelAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        ArgumentNullException.ThrowIfNull(destino);

        return Task.Run(() =>
        {
            var logo = CargadorLogoReporte.Cargar(reporte.RutaLogo, _logger);
            Escribir(reporte, logo, destino);
        }, cancellationToken);
    }

    internal static void Escribir(ReportePesajes r, LogoReporte? logo, Stream destino)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(TextosReporte.NombreHoja(r.EsAnuladas));
        var columnas = Columnas(r.EsAnuladas, r.MostrarDesposte);
        int n = columnas.Count;
        // Columna de los totales: la del total neto (no depende de que el desposte esté visible).
        int colT = columnas.FindIndex(c => c.Clave == "Total") + 1;

        // Fila 1: Empresa
        ws.Cell(1, 1).Value = r.Empresa;
        ws.Range(1, 1, 1, n).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        using var logoStream = logo is not null ? new MemoryStream(logo.Datos) : null;
        if (logo is not null && logoStream is not null)
        {
            ws.Row(1).Height = 36;
            var pic = ws.AddPicture(logoStream);
            var escala = Math.Min(120.0 / pic.OriginalWidth, 45.0 / pic.OriginalHeight);
            pic.MoveTo(ws.Cell(1, 1)).WithSize(
                (int)Math.Round(pic.OriginalWidth * escala),
                (int)Math.Round(pic.OriginalHeight * escala));
        }

        // Fila 2: Título
        ws.Cell(2, 1).Value = r.Titulo;
        ws.Range(2, 1, 2, n).Merge();
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 12;
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Fila 3: Subtítulo
        ws.Cell(3, 1).Value = r.Subtitulo;
        ws.Range(3, 1, 3, n).Merge();
        ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Fila 4: Generado
        ws.Cell(4, 1).Value = TextosReporte.Generado(r.GeneradoPor, r.GeneradoEnLocal);
        ws.Range(4, 1, 4, n).Merge();
        ws.Cell(4, 1).Style.Font.Italic = true;

        // Fila 5: vacía

        // Fila 6: Títulos de columnas
        for (int c = 1; c <= n; c++)
        {
            ws.Cell(6, c).Value = columnas[c - 1].Titulo;
        }

        var rangoTitulos = ws.Range(6, 1, 6, n);
        rangoTitulos.Style.Font.Bold = true;
        rangoTitulos.Style.Fill.BackgroundColor = XLColor.LightGray;
        rangoTitulos.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Fila 7..: Datos
        int u;
        if (r.Filas.Count == 0)
        {
            ws.Cell(7, 1).Value = TextosReporte.SinBoletas;
            ws.Cell(7, 1).Style.Font.Italic = true;
            u = 7;
        }
        else
        {
            for (int i = 0; i < r.Filas.Count; i++)
            {
                var f = r.Filas[i];
                int filaIdx = 7 + i;
                for (int c = 0; c < n; c++)
                {
                    columnas[c].Escribir(ws.Cell(filaIdx, c + 1), f);
                }
            }
            u = 6 + r.Filas.Count;
        }

        // Totales
        int ultimaFilaUsada;
        if (!r.EsAnuladas)
        {
            // u + 2: Total peso neto (kg)
            ws.Cell(u + 2, 1).Value = "Total peso neto (kg)";
            ws.Cell(u + 2, 1).Style.Font.Bold = true;
            ws.Cell(u + 2, colT).Value = r.Totales.TotalNetoKg;
            ws.Cell(u + 2, colT).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(u + 2, colT).Style.Font.Bold = true;

            // u + 3: Cantidad de reses
            ws.Cell(u + 3, 1).Value = "Cantidad de reses";
            ws.Cell(u + 3, 1).Style.Font.Bold = true;
            ws.Cell(u + 3, colT).Value = r.Totales.Reses;
            ws.Cell(u + 3, colT).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(u + 3, colT).Style.Font.Bold = true;

            // u + 4: Boletas
            ws.Cell(u + 4, 1).Value = "Boletas";
            ws.Cell(u + 4, 1).Style.Font.Bold = true;
            ws.Cell(u + 4, colT).Value = r.Totales.Boletas;
            ws.Cell(u + 4, colT).Style.Font.Bold = true;

            ultimaFilaUsada = u + 4;

            // u + 5: Res completa / media res (F11, D9: se omite si la empresa pesa un solo tipo y el reporte no mezcla tipos)
            if (r.MostrarDesposte)
            {
                ws.Cell(u + 5, 1).Value = "Res completa / media res";
                ws.Cell(u + 5, 1).Style.Font.Bold = true;
                ws.Cell(u + 5, colT).Value = $"{TextosReporte.Entero(r.Totales.ResesCompletas)} / {TextosReporte.Entero(r.Totales.MediasReses)}";
                ws.Cell(u + 5, colT).Style.Font.Bold = true;
                ultimaFilaUsada = u + 5;
            }
        }
        else
        {
            // u + 2: Boletas anuladas
            ws.Cell(u + 2, 1).Value = "Boletas anuladas";
            ws.Cell(u + 2, 1).Style.Font.Bold = true;
            ws.Cell(u + 2, colT).Value = r.Totales.Boletas;
            ws.Cell(u + 2, colT).Style.Font.Bold = true;

            // u + 3: Peso neto anulado (kg)
            ws.Cell(u + 3, 1).Value = "Peso neto anulado (kg)";
            ws.Cell(u + 3, 1).Style.Font.Bold = true;
            ws.Cell(u + 3, colT).Value = r.Totales.TotalNetoKg;
            ws.Cell(u + 3, colT).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(u + 3, colT).Style.Font.Bold = true;

            ultimaFilaUsada = u + 3;
        }

        var notaSimuladas = TextosReporte.NotaSimuladas(r.Totales.Simuladas);
        if (notaSimuladas is not null)
        {
            ultimaFilaUsada++;
            ws.Cell(ultimaFilaUsada, 1).Value = notaSimuladas;
            ws.Cell(ultimaFilaUsada, 1).Style.Font.Italic = true;
        }

        ws.Columns(1, n).AdjustToContents(6, ultimaFilaUsada);
        ws.SheetView.FreezeRows(6);
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.SetRowsToRepeatAtTop(6, 6);

        wb.SaveAs(destino);
    }

    private sealed record ColumnaExcel(string Clave, string Titulo, Action<IXLCell, FilaReporte> Escribir);

    private static ColumnaExcel Texto(string clave, string titulo, Func<FilaReporte, string> valor) =>
        new(clave, titulo, (celda, f) => celda.Value = valor(f));

    private static ColumnaExcel Kg(string clave, string titulo, Func<FilaReporte, decimal> valor) =>
        new(clave, titulo, (celda, f) =>
        {
            celda.Value = valor(f);
            celda.Style.NumberFormat.Format = "#,##0.0";
        });

    private static ColumnaExcel FechaHora(string clave, string titulo, Func<FilaReporte, DateTime?> valor) =>
        new(clave, titulo, (celda, f) =>
        {
            if (valor(f) is { } fecha)
            {
                celda.Value = fecha;
                celda.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            }
            else
            {
                celda.Value = "";
            }
        });

    // F11 (D9): las posiciones salen de esta lista; la columna "Tipo corte" se omite cuando el desposte no se muestra.
    private static List<ColumnaExcel> Columnas(bool anuladas, bool mostrarDesposte)
    {
        var desposte = Texto("Desposte", "Tipo corte", f => TextosBoleta.Desposte(f.TipoDesposte));

        var columnas = anuladas
            ? new List<ColumnaExcel>
            {
                new("Item", "Item", (c, f) => c.Value = f.Item),
                new("Numero", "N° boleta", (c, f) => c.Value = f.NumeroBoleta),
                FechaHora("Fecha", "Fecha", f => f.FechaHoraLocal),
                Texto("Codigo", "Código", f => f.CodigoGanadero),
                Texto("Ganadero", "Ganadero", f => f.NombreGanadero),
                new("CodigoCliente", "Cliente (código)", (c, f) => c.Value = f.CodigoCliente),
                Texto("Cliente", "Nombre del cliente", f => f.NombreCliente),
                desposte,
                Kg("Total", "Total neto (kg)", f => f.PesoTotal),
                FechaHora("AnuladaEl", "Anulada el", f => f.AnuladoEnLocal),
                Texto("AnuladaPor", "Anulada por", f => f.AnuladoPor ?? ""),
                Texto("Motivo", "Motivo", f => f.MotivoAnulacion ?? ""),
                Texto("Origen", "Origen", f => TextosReporte.Origen(f.Origen))
            }
            : new List<ColumnaExcel>
            {
                new("Item", "Item", (c, f) => c.Value = f.Item),
                new("Numero", "N° boleta", (c, f) => c.Value = f.NumeroBoleta),
                FechaHora("Fecha", "Fecha", f => f.FechaHoraLocal),
                new("Secuencia", "N° res (secuencia)", (c, f) => c.Value = f.Secuencia),
                Texto("Codigo", "Código", f => f.CodigoGanadero),
                Texto("Ganadero", "Ganadero", f => f.NombreGanadero),
                new("CodigoCliente", "Cliente (código)", (c, f) => c.Value = f.CodigoCliente),
                Texto("Cliente", "Nombre del cliente", f => f.NombreCliente),
                Texto("Fletero", "Fletero", f => f.NombreFletero),
                Texto("Direccion", "Dirección", f => f.Direccion),
                Kg("Izquierdo", "Izq. (kg)", f => f.PesoIzquierdo),
                Kg("Derecho", "Der. (kg)", f => f.PesoDerecho),
                Kg("Total", "Total neto (kg)", f => f.PesoTotal),
                desposte,
                Texto("Origen", "Origen", f => TextosReporte.Origen(f.Origen))
            };

        if (!mostrarDesposte)
        {
            columnas.Remove(desposte);
        }

        return columnas;
    }
}
