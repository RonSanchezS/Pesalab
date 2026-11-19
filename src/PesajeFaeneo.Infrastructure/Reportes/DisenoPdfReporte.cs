using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PesajeFaeneo.Infrastructure.Reportes;

internal static class DisenoPdfReporte
{
    static DisenoPdfReporte()
    {
        // QuestPDF exige declarar la licencia antes de generar (nota F7 de Agents.md). Aquí y no en Program.cs:
        // así la cumplen también los tests, que generan documentos sin pasar por el arranque.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    internal static Document Crear(ReportePesajes r, LogoReporte? logo)
    {
        return Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.Letter);
            p.Margin(1.2f, Unit.Centimetre);
            p.PageColor(Colors.White);
            p.DefaultTextStyle(x => x.FontSize(7.5f).FontFamily(Fonts.Arial));

            p.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(120).Height(45).AlignLeft().AlignMiddle().Image(logo.Imagen).FitArea();
                    }
                    row.RelativeItem().PaddingLeft(logo is null ? 0 : 8).AlignMiddle().Text(r.Empresa).FontSize(10).LetterSpacing(0.2f);
                });
                col.Item().PaddingTop(4).Border(1).PaddingVertical(2).AlignCenter().Text(r.Titulo).FontSize(12);
                col.Item().PaddingTop(2).AlignCenter().Text(r.Subtitulo).FontSize(9);
                col.Item().PaddingTop(6);
            });

            p.Content().Column(col =>
            {
                col.Item().Border(1).Table(t =>
                {
                    var columnas = ColumnasReporte.Para(r.EsAnuladas, r.MostrarDesposte);

                    t.ColumnsDefinition(cd =>
                    {
                        foreach (var columna in columnas)
                        {
                            if (columna.AnchoPt > 0)
                            {
                                cd.ConstantColumn(columna.AnchoPt);
                            }
                            else
                            {
                                cd.RelativeColumn();
                            }
                        }
                    });

                    t.Header(h =>
                    {
                        static IContainer EstiloEncabezado(IContainer c) =>
                            c.Background(Colors.Grey.Lighten3).BorderBottom(1).PaddingVertical(2).PaddingHorizontal(2);

                        bool algunGrupo = columnas.Any(cl => cl.Grupo != null);

                        if (algunGrupo)
                        {
                            for (int i = 0; i < columnas.Count; i++)
                            {
                                var cl = columnas[i];
                                if (cl.Grupo == null)
                                {
                                    var cell = h.Cell().RowSpan(2).Element(EstiloEncabezado).AlignBottom();
                                    if (cl.AlineadaDerecha)
                                    {
                                        cell.AlignRight().Text(cl.Titulo).Bold();
                                    }
                                    else
                                    {
                                        cell.Text(cl.Titulo).Bold();
                                    }
                                }
                                else
                                {
                                    string grupo = cl.Grupo;
                                    uint k = 1;
                                    while (i + 1 < columnas.Count && columnas[i + 1].Grupo == grupo)
                                    {
                                        k++;
                                        i++;
                                    }
                                    h.Cell().ColumnSpan(k).Element(EstiloEncabezado).AlignCenter().Text(grupo).Bold();
                                }
                            }

                            foreach (var cl in columnas.Where(cl => cl.Grupo != null))
                            {
                                var cell = h.Cell().Element(EstiloEncabezado);
                                if (cl.AlineadaDerecha)
                                {
                                    cell.AlignRight().Text(cl.Titulo).Bold();
                                }
                                else
                                {
                                    cell.Text(cl.Titulo).Bold();
                                }
                            }
                        }
                        else
                        {
                            foreach (var cl in columnas)
                            {
                                var cell = h.Cell().Element(EstiloEncabezado);
                                if (cl.AlineadaDerecha)
                                {
                                    cell.AlignRight().Text(cl.Titulo).Bold();
                                }
                                else
                                {
                                    cell.Text(cl.Titulo).Bold();
                                }
                            }
                        }
                    });

                    if (r.Filas.Count == 0)
                    {
                        t.Cell().ColumnSpan((uint)columnas.Count).PaddingVertical(6).AlignCenter().Text(TextosReporte.SinBoletas).Italic();
                    }
                    else
                    {
                        foreach (var fila in r.Filas)
                        {
                            var textos = ColumnasReporte.Textos(fila, r.EsAnuladas, r.MostrarDesposte);
                            for (int i = 0; i < columnas.Count; i++)
                            {
                                var celda = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2).PaddingHorizontal(2);
                                if (columnas[i].AlineadaDerecha)
                                {
                                    celda.AlignRight().Text(textos[i]);
                                }
                                else
                                {
                                    celda.Text(textos[i]);
                                }
                            }
                        }
                    }
                });

                col.Item().PaddingTop(8).LineHorizontal(0.5f);

                if (!r.EsAnuladas)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(TextosReporte.TotalPesoNeto(r.Totales)).Bold().FontSize(10);
                        row.RelativeItem().AlignRight().Text(TextosReporte.CantidadReses(r.Totales)).Bold().FontSize(10);
                    });
                    col.Item().Text(TextosReporte.DetalleBoletas(r.Totales, r.MostrarDesposte)).FontSize(8);
                }
                else
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(TextosReporte.BoletasAnuladas(r.Totales)).Bold().FontSize(10);
                        row.RelativeItem().AlignRight().Text(TextosReporte.PesoNetoAnulado(r.Totales)).Bold().FontSize(10);
                    });
                }

                var notaSimuladas = TextosReporte.NotaSimuladas(r.Totales.Simuladas);
                if (notaSimuladas is not null)
                {
                    col.Item().PaddingTop(4).Text(notaSimuladas).Italic().FontColor(Colors.Red.Darken2);
                }
            });

            p.Footer().Row(row =>
            {
                row.RelativeItem().Text(TextosReporte.Generado(r.GeneradoPor, r.GeneradoEnLocal)).FontSize(7);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        })).WithMetadata(new DocumentMetadata { Title = r.Titulo, Author = r.Empresa, Creator = "PesajeFaeneo" });
    }
}
