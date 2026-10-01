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
        int n = r.EsAnuladas ? 13 : 14;

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
        string[] titulos = r.EsAnuladas
            ?
            [
                "Item", "N° boleta", "Fecha", "Marca", "Ganadero",
                "Cliente (código)", "Nombre del cliente", "Tipo corte", "Total neto (kg)",
                "Anulada el", "Anulada por", "Motivo", "Origen"
            ]
            :
            [
                "Item", "N° boleta", "Fecha", "N° res (secuencia)", "Marca", "Ganadero",
                "Cliente (código)", "Nombre del cliente", "Dirección", "Izq. (kg)", "Der. (kg)",
                "Total neto (kg)", "Tipo corte", "Origen"
            ];

        for (int c = 1; c <= n; c++)
        {
            ws.Cell(6, c).Value = titulos[c - 1];
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

                if (!r.EsAnuladas)
                {
                    ws.Cell(filaIdx, 1).Value = f.Item;
                    ws.Cell(filaIdx, 2).Value = f.NumeroBoleta;
                    ws.Cell(filaIdx, 3).Value = f.FechaHoraLocal;
                    ws.Cell(filaIdx, 3).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                    ws.Cell(filaIdx, 4).Value = f.Secuencia;
                    ws.Cell(filaIdx, 5).Value = f.MarcaGanadero;
                    ws.Cell(filaIdx, 6).Value = f.NombreGanadero;
                    ws.Cell(filaIdx, 7).Value = f.CodigoCliente;
                    ws.Cell(filaIdx, 8).Value = f.NombreCliente;
                    ws.Cell(filaIdx, 9).Value = f.Direccion;
                    ws.Cell(filaIdx, 10).Value = f.PesoIzquierdo;
                    ws.Cell(filaIdx, 10).Style.NumberFormat.Format = "#,##0.0";
                    ws.Cell(filaIdx, 11).Value = f.PesoDerecho;
                    ws.Cell(filaIdx, 11).Style.NumberFormat.Format = "#,##0.0";
                    ws.Cell(filaIdx, 12).Value = f.PesoTotal;
                    ws.Cell(filaIdx, 12).Style.NumberFormat.Format = "#,##0.0";
                    ws.Cell(filaIdx, 13).Value = TextosBoleta.Desposte(f.TipoDesposte);
                    ws.Cell(filaIdx, 14).Value = TextosReporte.Origen(f.Origen);
                }
                else
                {
                    ws.Cell(filaIdx, 1).Value = f.Item;
                    ws.Cell(filaIdx, 2).Value = f.NumeroBoleta;
                    ws.Cell(filaIdx, 3).Value = f.FechaHoraLocal;
                    ws.Cell(filaIdx, 3).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                    ws.Cell(filaIdx, 4).Value = f.MarcaGanadero;
                    ws.Cell(filaIdx, 5).Value = f.NombreGanadero;
                    ws.Cell(filaIdx, 6).Value = f.CodigoCliente;
                    ws.Cell(filaIdx, 7).Value = f.NombreCliente;
                    ws.Cell(filaIdx, 8).Value = TextosBoleta.Desposte(f.TipoDesposte);
                    ws.Cell(filaIdx, 9).Value = f.PesoTotal;
                    ws.Cell(filaIdx, 9).Style.NumberFormat.Format = "#,##0.0";
                    if (f.AnuladoEnLocal.HasValue)
                    {
                        ws.Cell(filaIdx, 10).Value = f.AnuladoEnLocal.Value;
                        ws.Cell(filaIdx, 10).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                    }
                    else
                    {
                        ws.Cell(filaIdx, 10).Value = "";
                    }
                    ws.Cell(filaIdx, 11).Value = f.AnuladoPor ?? "";
                    ws.Cell(filaIdx, 12).Value = f.MotivoAnulacion ?? "";
                    ws.Cell(filaIdx, 13).Value = TextosReporte.Origen(f.Origen);
                }
            }
            u = 6 + r.Filas.Count;
        }

        // Totales
        int ultimaFilaUsada;
        if (!r.EsAnuladas)
        {
            int colT = 12;

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

            // u + 5: Res completa / media res
            ws.Cell(u + 5, 1).Value = "Res completa / media res";
            ws.Cell(u + 5, 1).Style.Font.Bold = true;
            ws.Cell(u + 5, colT).Value = $"{TextosReporte.Entero(r.Totales.ResesCompletas)} / {TextosReporte.Entero(r.Totales.MediasReses)}";
            ws.Cell(u + 5, colT).Style.Font.Bold = true;

            ultimaFilaUsada = u + 5;
        }
        else
        {
            int colT = 9;

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
}
