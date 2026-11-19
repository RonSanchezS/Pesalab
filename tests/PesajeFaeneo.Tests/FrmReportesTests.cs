using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Printing;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmReportesTests
{
    private static FakeTimeProvider CrearTimeProvider()
    {
        // 2026-09-28T02:00:00Z en UTC-4 es 2026-09-27T22:00:00 local (hoy local = 27/09/2026)
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 2, 0, 0, TimeSpan.Zero));
        tp.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));
        return tp;
    }

    private static void CompletarBombeando(Task t)
    {
        var sw = Stopwatch.StartNew();
        while (!t.IsCompleted && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(t.IsCompleted, "La tarea no completó en el tiempo esperado.");
        t.GetAwaiter().GetResult();
    }

    [Fact]
    public void AlCargar_InicializaFechasYListasSinGenerar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Opciones = new OpcionesReportes(
                    [new ElementoBusqueda(1, "GAN1", "GANADERO UNO")],
                    [new ElementoBusqueda(2, "CLI1", "CLIENTE UNO")])
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(servicio.FiltrosRecibidos);
            Assert.Equal(new DateTime(2026, 9, 27), form.dtpDesde.Value.Date);
            Assert.Equal(new DateTime(2026, 9, 27), form.dtpHasta.Value.Date);
            Assert.False(form.dtpDesde.ShowCheckBox);
            Assert.False(form.dtpHasta.ShowCheckBox);
            form.ctlGanadero.Seleccionar(1);
            Assert.NotNull(form.ctlGanadero.Seleccion);
            Assert.Equal(1, form.ctlGanadero.Seleccion.Id);

            form.ctlCliente.Seleccionar(2);
            Assert.NotNull(form.ctlCliente.Seleccion);
            Assert.Equal(2, form.ctlCliente.Seleccion.Id);
        });
    }

    [Fact]
    public void SeleccionDeTipo_PorDefectoPesajesDelDia_ConfigurarCambiaTipo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            Assert.Equal(0, form.cmbTipo.SelectedIndex); // PesajesDelDia

            form.Configurar(TipoReporte.Anuladas);
            Assert.Equal(5, form.cmbTipo.SelectedIndex);

            form.Configurar(TipoReporte.PorRaza);
            Assert.Equal(4, form.cmbTipo.SelectedIndex);

            form.Configurar(TipoReporte.PesajesEntreFechas);
            Assert.Equal(1, form.cmbTipo.SelectedIndex);
        });
    }

    [Fact]
    public void HabilitacionControlesPorTipo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // PesajesDelDia
            form.Configurar(TipoReporte.PesajesDelDia);
            Assert.False(form.dtpHasta.Enabled);
            Assert.False(form.ctlGanadero.Enabled);
            Assert.False(form.ctlCliente.Enabled);

            // PorGanadero
            form.Configurar(TipoReporte.PorGanadero);
            Assert.True(form.dtpHasta.Enabled);
            Assert.True(form.ctlGanadero.Enabled);
            Assert.False(form.ctlCliente.Enabled);

            // PorCliente
            form.Configurar(TipoReporte.PorCliente);
            Assert.True(form.dtpHasta.Enabled);
            Assert.False(form.ctlGanadero.Enabled);
            Assert.True(form.ctlCliente.Enabled);

            // PesajesEntreFechas
            form.Configurar(TipoReporte.PesajesEntreFechas);
            Assert.True(form.dtpHasta.Enabled);
            Assert.False(form.ctlGanadero.Enabled);
            Assert.False(form.ctlCliente.Enabled);

            // Anuladas
            form.Configurar(TipoReporte.Anuladas);
            Assert.True(form.dtpHasta.Enabled);
            Assert.False(form.ctlGanadero.Enabled);
            Assert.False(form.ctlCliente.Enabled);
        });
    }

    [Fact]
    public void ConstruirFiltro_SoloEnviaIdsParaSuTipo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.ctlGanadero.EstablecerElementos([new ElementoBusqueda(10, "G1", "GANADERO")]);
            form.ctlGanadero.Seleccionar(10);
            form.ctlCliente.EstablecerElementos([new ElementoBusqueda(20, "C1", "CLIENTE")]);
            form.ctlCliente.Seleccionar(20);

            // PorGanadero
            form.Configurar(TipoReporte.PorGanadero);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            var filtro1 = servicio.FiltrosRecibidos.Last();
            Assert.Equal(10, filtro1.GanaderoId);
            Assert.Null(filtro1.ClienteId);

            // PesajesEntreFechas: aunque haya ganadero y cliente seleccionados, envía ambos null
            form.Configurar(TipoReporte.PesajesEntreFechas);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            var filtro2 = servicio.FiltrosRecibidos.Last();
            Assert.Null(filtro2.GanaderoId);
            Assert.Null(filtro2.ClienteId);
        });
    }

    [Fact]
    public void Generar_MuestraFilasTotalesYHabilitaAcciones()
    {
        HiloSta.Ejecutar(() =>
        {
            var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5);
            var servicio = new ServicioReportesFalso { Resultado = reporte };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(5, form.dgvVista.Rows.Count);
            Assert.IsType<FilaReporte>(form.dgvVista.Rows[0].Tag);
            Assert.Contains(TextosReporte.TotalPesoNeto(reporte.Totales), form.lblTotales.Text);
            Assert.Contains(TextosReporte.CantidadReses(reporte.Totales), form.lblTotales.Text);

            Assert.True(form.btnPdf.Enabled);
            Assert.True(form.btnExcel.Enabled);
            Assert.True(form.btnVistaPrevia.Enabled);
            Assert.True(form.btnImprimir.Enabled);
        });
    }

    [Fact]
    public void Generar_ReporteAnuladas_MuestraColumnasAnuladasYRestableceNormal()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // Anuladas
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 3);
            form.Configurar(TipoReporte.Anuladas);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(form.dgvVista.Columns["colMotivo"]);
            Assert.Null(form.dgvVista.Columns["colDireccion"]);

            // Normal
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3);
            form.Configurar(TipoReporte.PesajesEntreFechas);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(form.dgvVista.Columns["colDireccion"]);
            Assert.Null(form.dgvVista.Columns["colMotivo"]);
        });
    }

    [Fact]
    public void Generar_ValidacionException_MuestraErrorEnControlYDeshabilitaAcciones()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Excepcion = new ValidacionException(CamposReportes.Ganadero, "Debe seleccionar un ganadero.")
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // Primero hay un reporte bueno en pantalla: el error debe LIMPIARLO (no basta con que ya estuviera vacío).
            var excepcion = servicio.Excepcion;
            servicio.Excepcion = null;
            form.Configurar(TipoReporte.PorGanadero);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotNull(form.ReporteActual);
            Assert.NotEmpty(form.dgvVista.Rows);
            Assert.True(form.btnPdf.Enabled);

            servicio.Excepcion = excepcion;
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Null(form.ReporteActual);
            Assert.Empty(form.dgvVista.Rows);
            Assert.False(form.btnPdf.Enabled);
            Assert.False(form.btnExcel.Enabled);
            Assert.Equal("Debe seleccionar un ganadero.", form.ErrorDe(form.ctlGanadero));
            Assert.Equal("", form.ErrorDe(form.dtpHasta));
        });
    }

    [Fact]
    public void Generar_ValidacionDeFechaHasta_MuestraErrorEnDtpHastaConElTextoDelMaximo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Excepcion = new ValidacionException(CamposReportes.FechaHasta, TextosReporte.MaximoSuperado)
            };

            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.Configurar(TipoReporte.PesajesEntreFechas);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(TextosReporte.MaximoSuperado, form.ErrorDe(form.dtpHasta));
            Assert.Null(form.ReporteActual);
            Assert.False(form.btnPdf.Enabled);
        });
    }

    [Fact]
    public void Generar_ReporteVacio_MuestraSinBoletasYDeshabilitaAcciones()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 0)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(form.dgvVista.Rows);
            Assert.Contains(TextosReporte.SinBoletas, form.lblAviso.Text);
            Assert.False(form.btnPdf.Enabled);
            Assert.False(form.btnExcel.Enabled);
            Assert.False(form.btnVistaPrevia.Enabled);
            Assert.False(form.btnImprimir.Enabled);
        });
    }

    [Fact]
    public void Generar_AvisosSegunCantidadDeFilas()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // 1500 filas
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1500);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ReglasReportes.MaximoFilasVista, form.dgvVista.Rows.Count);
            Assert.Contains("primeras 1.000 boletas", form.lblAviso.Text);
            Assert.Contains("1.500", form.lblAviso.Text);
            // R43: los totales salen de reporte.Totales (1 500 boletas), no de las 1 000 filas de la grilla.
            Assert.Contains(TextosReporte.TotalPesoNeto(servicio.Resultado.Totales), form.lblTotales.Text);
            Assert.Contains(TextosReporte.CantidadReses(servicio.Resultado.Totales), form.lblTotales.Text);
            Assert.Contains("Boletas: 1.500", form.lblTotales.Text);
            Assert.True(form.btnVistaPrevia.Enabled);
            Assert.True(form.btnImprimir.Enabled);

            // 2500 filas: excede MaximoFilasImpresion (2000)
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2500);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("primeras 1.000 boletas", form.lblAviso.Text);
            Assert.Contains("Boletas: 2.500", form.lblTotales.Text);
            Assert.Contains("Para imprimir más de 2.000 boletas", form.lblAviso.Text);
            Assert.False(form.btnVistaPrevia.Enabled);
            Assert.False(form.btnImprimir.Enabled);
            Assert.True(form.btnPdf.Enabled);
            Assert.True(form.btnExcel.Enabled);
        });
    }

    [Fact]
    public void Generar_AvisoLogoSegunRutaLogo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // Sin logo
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: null);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.Contains("No hay un logo configurado", form.lblAviso.Text);
            Assert.True(form.btnPdf.Enabled);

            // Con logo
            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: "C:\\logo.png");
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.DoesNotContain("No hay un logo configurado", form.lblAviso.Text);
            Assert.True(form.btnPdf.Enabled);
        });
    }

    [Fact]
    public void InvalidarReporte_AlModificarFiltros_LimpiaVistaYAvisa()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(form.ReporteActual);
            Assert.NotEmpty(form.dgvVista.Rows);

            // Modificar dtpDesde
            form.dtpDesde.Value = form.dtpDesde.Value.AddDays(1);

            Assert.Null(form.ReporteActual);
            Assert.Empty(form.dgvVista.Rows);
            Assert.False(form.btnPdf.Enabled);
            Assert.Contains("Los filtros cambiaron", form.lblAviso.Text);
        });
    }

    [Fact]
    public void Generar_RespuestaTardia_SeDescarta()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var servicio = new ServicioReportesFalso
            {
                PausaGenerarAsync = () => tcs.Task,
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            // Iniciar generación asíncrona que queda pausada
            var tareaGenerar = form.GenerarParaTestAsync();

            // Cambiar filtro (invalida y cambia versión de generación)
            form.dtpDesde.Value = form.dtpDesde.Value.AddDays(2);

            // Liberar la respuesta del servicio
            tcs.SetResult(true);
            CompletarBombeando(tareaGenerar);

            // La vista debe seguir limpia
            Assert.Null(form.ReporteActual);
            Assert.Empty(form.dgvVista.Rows);
        });
    }

    [Fact]
    public void ExportarPdf_GeneraArchivoInvocaPreguntarYAbre()
    {
        string dirTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-exp-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirTemp);

        try
        {
            HiloSta.Ejecutar(() =>
            {
                var servicio = new ServicioReportesFalso
                {
                    Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
                };
                var pdf = new GeneradorPdfReportesFalso();
                var excel = new GeneradorExcelReportesFalso();
                var tp = CrearTimeProvider();

                using var form = new FrmReportes(servicio, pdf, excel, tp);
                form.GenerarParaTestAsync().GetAwaiter().GetResult();

                string destino = Path.Combine(dirTemp, "reporte.pdf");
                string? rutaPreguntada = null;
                string? rutaAbierta = null;
                string? nombreSugerido = null;
                string? filtroDialogo = null;

                form.ElegirRutaGuardarParaTest = (nom, fil) => { nombreSugerido = nom; filtroDialogo = fil; return destino; };
                form.PreguntarAbrirParaTest = r => { rutaPreguntada = r; return true; };
                form.AbrirArchivoParaTest = r => { rutaAbierta = r; };

                form.ExportarPdfParaTestAsync().GetAwaiter().GetResult();

                Assert.True(File.Exists(destino));
                Assert.Equal("%PDF-falso", File.ReadAllText(destino));
                Assert.Same(form.ReporteActual, pdf.ReportesPdf[0]);
                Assert.Equal(TextosReporte.NombreArchivo(form.ReporteActual!, ".pdf"), nombreSugerido);
                Assert.Contains("*.pdf", filtroDialogo);
                Assert.Equal(destino, rutaPreguntada);
                Assert.Equal(destino, rutaAbierta);

                // Si el usuario responde "No" a abrirlo, no se lanza ningún proceso.
                string? abiertoDeNuevo = null;
                form.PreguntarAbrirParaTest = _ => false;
                form.AbrirArchivoParaTest = r => { abiertoDeNuevo = r; };
                form.ExportarPdfParaTestAsync().GetAwaiter().GetResult();
                Assert.Null(abiertoDeNuevo);
            });
        }
        finally
        {
            if (Directory.Exists(dirTemp))
            {
                Directory.Delete(dirTemp, true);
            }
        }
    }

    [Fact]
    public void ExportarExcel_GeneraArchivoXlsx()
    {
        string dirTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-exp-xl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirTemp);

        try
        {
            HiloSta.Ejecutar(() =>
            {
                var servicio = new ServicioReportesFalso
                {
                    Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
                };
                var pdf = new GeneradorPdfReportesFalso();
                var excel = new GeneradorExcelReportesFalso();
                var tp = CrearTimeProvider();

                using var form = new FrmReportes(servicio, pdf, excel, tp);
                form.GenerarParaTestAsync().GetAwaiter().GetResult();

                string destino = Path.Combine(dirTemp, "reporte.xlsx");
                string? nombreSugerido = null;
                string? filtroDialogo = null;
                string? rutaAbierta = null;
                form.ElegirRutaGuardarParaTest = (nom, fil) => { nombreSugerido = nom; filtroDialogo = fil; return destino; };
                form.PreguntarAbrirParaTest = _ => false;
                form.AbrirArchivoParaTest = r => { rutaAbierta = r; };

                form.ExportarExcelParaTestAsync().GetAwaiter().GetResult();

                Assert.True(File.Exists(destino));
                Assert.Equal("PK-falso", File.ReadAllText(destino));
                Assert.Same(form.ReporteActual, excel.ReportesExcel[0]);
                Assert.Equal(TextosReporte.NombreArchivo(form.ReporteActual!, ".xlsx"), nombreSugerido);
                Assert.Contains("*.xlsx", filtroDialogo);
                Assert.Null(rutaAbierta);
            });
        }
        finally
        {
            if (Directory.Exists(dirTemp))
            {
                Directory.Delete(dirTemp, true);
            }
        }
    }

    [Fact]
    public void Exportar_ElegirRutaCancelado_NoGenera()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            form.ElegirRutaGuardarParaTest = (nom, fil) => null;

            form.ExportarPdfParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(pdf.ReportesPdf);
        });
    }

    [Fact]
    public void Exportar_ArchivoBloqueado_MuestraAdvertenciaSinLanzar()
    {
        string dirTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-bloq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirTemp);

        try
        {
            HiloSta.Ejecutar(() =>
            {
                var servicio = new ServicioReportesFalso
                {
                    Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
                };
                var pdf = new GeneradorPdfReportesFalso();
                var excel = new GeneradorExcelReportesFalso();
                var tp = CrearTimeProvider();

                using var form = new FrmReportes(servicio, pdf, excel, tp);
                form.GenerarParaTestAsync().GetAwaiter().GetResult();

                string destino = Path.Combine(dirTemp, "bloqueado.pdf");
                string? mensajeAdvertencia = null;
                bool preguntoAbrir = false;

                using (var fs = new FileStream(destino, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    form.ElegirRutaGuardarParaTest = (nom, fil) => destino;
                    form.MostrarAdvertenciaParaTest = msg => mensajeAdvertencia = msg;
                    form.PreguntarAbrirParaTest = _ => { preguntoAbrir = true; return false; };

                    form.ExportarPdfParaTestAsync().GetAwaiter().GetResult();
                }

                Assert.NotNull(mensajeAdvertencia);
                Assert.Contains("No se pudo guardar el archivo", mensajeAdvertencia);
                Assert.False(preguntoAbrir);
            });
        }
        finally
        {
            if (Directory.Exists(dirTemp))
            {
                Directory.Delete(dirTemp, true);
            }
        }
    }

    [Fact]
    public void VistaPrevia_InvocaGeneradorPaginasYFrmVistaPrevia()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Form? formularioDialogo = null;
            form.MostrarDialogoParaTest = f => { formularioDialogo = f; return System.Windows.Forms.DialogResult.OK; };

            form.VistaPreviaParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(ReglasReportes.ResolucionImpresionDpi, Assert.Single(pdf.DpiRecibidos));
            Assert.IsType<FrmVistaPrevia>(formularioDialogo);
        });
    }

    [Fact]
    public void Imprimir_InvocaElegirImpresoraYEnviarAImpresora()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            PrintDocument? documentoElegido = null;
            int enviados = 0;
            string? advertenciaImpresion = null;
            bool elegir = false;

            form.ElegirImpresoraParaTest = doc => { documentoElegido = doc; return elegir; };
            form.EnviarAImpresoraParaTest = doc => { enviados++; };

            // Si el usuario cancela el diálogo de impresora, nada se envía.
            form.ImprimirParaTestAsync().GetAwaiter().GetResult();
            Assert.IsType<PesajeFaeneo.WinForms.Impresion.DocumentoPaginasReporte>(documentoElegido);
            Assert.Equal(0, enviados);

            // Si acepta, se envía exactamente una vez.
            elegir = true;
            form.ImprimirParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(1, enviados);

            // Con error Win32Exception
            form.EnviarAImpresoraParaTest = doc => throw new Win32Exception(5);
            form.MostrarAdvertenciaParaTest = msg => advertenciaImpresion = msg;

            form.ImprimirParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(advertenciaImpresion);
            Assert.StartsWith("No se pudo imprimir el reporte: ", advertenciaImpresion);
        });
    }

    [Fact]
    public void ExportarPdf_Reentrada_IgnoraSegundaInvocacion()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var servicio = new ServicioReportesFalso
            {
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            var pdf = new GeneradorPdfReportesFalso
            {
                PausaPdfAsync = () => tcs.Task
            };
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            string rutaTemp = Path.Combine(Path.GetTempPath(), "pesalab-reentrada-" + Guid.NewGuid().ToString("N") + ".pdf");
            form.ElegirRutaGuardarParaTest = (nom, fil) => rutaTemp;
            form.PreguntarAbrirParaTest = _ => false;

            var t1 = form.ExportarPdfParaTestAsync();
            var t2 = form.ExportarPdfParaTestAsync();

            tcs.SetResult(true);

            CompletarBombeando(t1);
            CompletarBombeando(t2);

            Assert.Single(pdf.ReportesPdf);

            try { File.Delete(rutaTemp); } catch { }
        });
    }

    [Fact]
    public void Teclado_AcceptButtonYCancelButton_EnterEnGrillaNoGenera()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            Assert.Same(form.btnGenerar, form.AcceptButton);
            Assert.Same(form.btnCerrar, form.CancelButton);

            form.Show();
            form.dgvVista.Focus();

            // Con la grilla enfocada, Enter se consume sin acción.
            bool procesado = form.ProcesarTeclaParaTest(System.Windows.Forms.Keys.Enter);
            Assert.True(procesado);
            Assert.Empty(servicio.FiltrosRecibidos);

            // Control negativo: con el foco fuera de la grilla, Enter NO se consume (lo atiende el AcceptButton).
            form.cmbTipo.Focus();
            Assert.False(form.ProcesarTeclaParaTest(System.Windows.Forms.Keys.Enter));
        });
    }

    [Fact]
    public void CambiarTipoADelDia_ConReporteGenerado_ConservaElAvisoDeFiltrosCambiados()
    {
        // Regresión: ActualizarControlesPorTipo() ponía dtpHasta = dtpDesde y el ValueChanged de dtpHasta invalidaba
        // primero; el cambio de tipo invalidaba otra vez con _reporte ya nulo y borraba el aviso.
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5) };
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.Configurar(TipoReporte.PesajesEntreFechas);
            form.dtpHasta.Value = form.dtpDesde.Value.AddDays(3);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotNull(form.ReporteActual);

            form.Configurar(TipoReporte.PesajesDelDia);

            Assert.Null(form.ReporteActual);
            Assert.Empty(form.dgvVista.Rows);
            Assert.Contains("Los filtros cambiaron", form.lblAviso.Text);
        });
    }

    [Fact]
    public void Invalidar_CambioDeTipoGanaderoOCliente_LimpiaLaVista()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Opciones = new OpcionesReportes(
                    [new ElementoBusqueda(1, "G1", "Ganadero Uno", null, true)],
                    [new ElementoBusqueda(2, "101", "Cliente Uno", null, true)]),
                Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5)
            };
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            void GenerarYVerificarLimpieza(Action cambio)
            {
                form.GenerarParaTestAsync().GetAwaiter().GetResult();
                Assert.NotNull(form.ReporteActual);
                cambio();
                Assert.Null(form.ReporteActual);
                Assert.Empty(form.dgvVista.Rows);
                Assert.False(form.btnPdf.Enabled);
                Assert.Contains("Los filtros cambiaron", form.lblAviso.Text);
            }

            form.Configurar(TipoReporte.PesajesEntreFechas);
            GenerarYVerificarLimpieza(() => form.Configurar(TipoReporte.PorGanadero));
            GenerarYVerificarLimpieza(() => form.ctlGanadero.Seleccionar(1));
            form.Configurar(TipoReporte.PorCliente);
            GenerarYVerificarLimpieza(() => form.ctlCliente.Seleccionar(2));
        });
    }

    [Fact]
    public void PesajesDelDia_HastaSigueADesdeYElFiltroEnviadoLoRefleja()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.Configurar(TipoReporte.PesajesDelDia);

            form.dtpDesde.Value = new DateTime(2026, 9, 10);

            Assert.Equal(new DateTime(2026, 9, 10), form.dtpHasta.Value.Date);
            Assert.False(form.dtpHasta.Enabled);

            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            var filtro = Assert.Single(servicio.FiltrosRecibidos);
            Assert.Equal(new DateOnly(2026, 9, 10), filtro.Desde);
            Assert.Equal(filtro.Desde, filtro.Hasta);
        });
    }

    [Fact]
    public void ControlDeshabilitado_ConservaSuSeleccionAlCambiarDeTipo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Opciones = new OpcionesReportes([new ElementoBusqueda(1, "G1", "Ganadero Uno", null, true)], [])
            };
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.Configurar(TipoReporte.PorGanadero);
            form.ctlGanadero.Seleccionar(1);
            form.Configurar(TipoReporte.PesajesEntreFechas);

            Assert.False(form.ctlGanadero.Enabled);
            Assert.Equal(1, form.ctlGanadero.Seleccion?.Id);
        });
    }

    [Fact]
    public void ExportarPdf_SiUnFiltroCambiaDuranteLaGeneracion_LosBotonesNoQuedanHabilitadosSinReporte()
    {
        // Regresión R54: EjecutarAsync restaura el Enabled previo del botón al terminar; si un filtro cambió mientras
        // tanto, btnPdf volvía a quedar habilitado con ReporteActual == null.
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var pdf = new GeneradorPdfReportesFalso { PausaPdfAsync = () => tcs.Task };
            var servicio = new ServicioReportesFalso { Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5) };
            using var form = new FrmReportes(servicio, pdf, new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            string ruta = Path.Combine(Path.GetTempPath(), "pesalab-r54-" + Guid.NewGuid().ToString("N") + ".pdf");
            form.ElegirRutaGuardarParaTest = (_, _) => ruta;
            form.PreguntarAbrirParaTest = _ => false;

            try
            {
                var t = form.ExportarPdfParaTestAsync();
                form.dtpDesde.Value = form.dtpDesde.Value.AddDays(1);   // invalida mientras se genera
                tcs.SetResult(true);
                CompletarBombeando(t);

                Assert.Null(form.ReporteActual);
                Assert.False(form.btnPdf.Enabled);
                Assert.False(form.btnExcel.Enabled);
                Assert.False(form.btnVistaPrevia.Enabled);
                Assert.False(form.btnImprimir.Enabled);
            }
            finally
            {
                try { File.Delete(ruta); } catch (IOException) { }
            }
        });
    }

    [Fact]
    public void Exportar_MientrasOtraExportacionSigueEnCurso_NoAbreOtroDialogoDeGuardado()
    {
        // Si se abriera el SaveFileDialog del segundo botón, el EjecutarAsync posterior se descartaría en silencio
        // (FormBase ocupado) y el usuario creería haber guardado un archivo que no existe.
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var pdf = new GeneradorPdfReportesFalso { PausaPdfAsync = () => tcs.Task };
            var excel = new GeneradorExcelReportesFalso();
            var servicio = new ServicioReportesFalso { Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5) };
            using var form = new FrmReportes(servicio, pdf, excel, CrearTimeProvider());
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            int dialogosAbiertos = 0;
            string ruta = Path.Combine(Path.GetTempPath(), "pesalab-conc-" + Guid.NewGuid().ToString("N") + ".pdf");
            form.ElegirRutaGuardarParaTest = (_, _) => { dialogosAbiertos++; return ruta; };
            form.PreguntarAbrirParaTest = _ => false;

            try
            {
                var tPdf = form.ExportarPdfParaTestAsync();
                var tExcel = form.ExportarExcelParaTestAsync();

                tcs.SetResult(true);
                CompletarBombeando(tPdf);
                CompletarBombeando(tExcel);

                Assert.Equal(1, dialogosAbiertos);
                Assert.Single(pdf.ReportesPdf);
                Assert.Empty(excel.ReportesExcel);
            }
            finally
            {
                try { File.Delete(ruta); } catch (IOException) { }
            }
        });
    }

    [Fact]
    public void Generar_ErrorDeValidacionTardio_NoPintaErroresSobreLosFiltrosNuevos()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var servicio = new ServicioReportesFalso
            {
                PausaGenerarAsync = () => tcs.Task,
                Excepcion = new ValidacionException(CamposReportes.Ganadero, "Seleccione el ganadero.")
            };
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.Configurar(TipoReporte.PorGanadero);

            var t = form.GenerarParaTestAsync();
            form.dtpDesde.Value = form.dtpDesde.Value.AddDays(1);   // el usuario cambió un filtro: la respuesta ya es obsoleta
            tcs.SetResult(true);
            CompletarBombeando(t);

            Assert.Equal("", form.ErrorDe(form.ctlGanadero));
        });
    }

    [Fact]
    public void Controles_SoloLecturaYBotonesEspecificados()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            var pdf = new GeneradorPdfReportesFalso();
            var excel = new GeneradorExcelReportesFalso();
            var tp = CrearTimeProvider();

            using var form = new FrmReportes(servicio, pdf, excel, tp);

            var botones = new List<System.Windows.Forms.Button>();
            void BuscarBotones(System.Windows.Forms.Control ctl)
            {
                if (ctl is System.Windows.Forms.Button b)
                {
                    botones.Add(b);
                }
                foreach (System.Windows.Forms.Control hijo in ctl.Controls)
                {
                    BuscarBotones(hijo);
                }
            }
            BuscarBotones(form);

            // Exactamente los 6 botones esperados: Generar, Vista Previa, Imprimir, PDF, Excel, Cerrar
            Assert.Equal(6, botones.Count);
            Assert.Contains(form.btnGenerar, botones);
            Assert.Contains(form.btnVistaPrevia, botones);
            Assert.Contains(form.btnImprimir, botones);
            Assert.Contains(form.btnPdf, botones);
            Assert.Contains(form.btnExcel, botones);
            Assert.Contains(form.btnCerrar, botones);

            // Grilla de solo lectura
            Assert.True(form.dgvVista.ReadOnly);
            Assert.False(form.dgvVista.AllowUserToAddRows);
            Assert.False(form.dgvVista.AllowUserToDeleteRows);
        });
    }

    // F11 (D6, D9): la columna "Tipo Corte" y el desglose por tipo dependen de cada reporte generado.
    [Fact]
    public void Generar_SinDesposte_OcultaTipoCorteYDesglose_YLoRecuperaConElSiguienteReporte()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso();
            using var form = new FrmReportes(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());
            form.Configurar(TipoReporte.PesajesEntreFechas);

            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3) with { MostrarDesposte = false };
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Null(form.dgvVista.Columns["colDesposte"]);
            Assert.Equal(ColumnasReporte.Para(false, false).Count, form.dgvVista.Columns.Count);
            Assert.Equal(TextosReporte.Kg(servicio.Resultado.Filas[0].PesoTotal), form.dgvVista.Rows[0].Cells["colTotal"].Value);
            Assert.Contains("Boletas: 3", form.lblTotales.Text);
            Assert.DoesNotContain("media res", form.lblTotales.Text);

            servicio.Resultado = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(form.dgvVista.Columns["colDesposte"]);
            Assert.Contains("media res", form.lblTotales.Text);
        });
    }
}
