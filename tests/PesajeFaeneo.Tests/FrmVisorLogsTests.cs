using System.Windows.Forms;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Logs;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmVisorLogsTests
{
    [Fact]
    public void Cargar_LlenaComboYLeePrimerArchivo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog>
                {
                    new("pesaje-20261001.log", new DateOnly(2026, 10, 1), 1024 * 1024),
                    new("pesaje-20260930.log", new DateOnly(2026, 9, 30), 2048 * 1024)
                },
                Contenido = new ContenidoLog("pesaje-20261001.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Inicio del sistema", ""),
                    new(2, "10:05:00", NivelLog.Advertencia, "Balanza inestable", "Reintento 1")
                }, false, 1024 * 1024)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(2, form.cmbArchivo.Items.Count);
            Assert.Equal(0, form.cmbArchivo.SelectedIndex);
            Assert.Single(servicio.ArchivosLeidos);
            Assert.Equal("pesaje-20261001.log", servicio.ArchivosLeidos[0]);
            Assert.Equal(2, form.dgvEntradas.Rows.Count);
            Assert.Contains("Mostrando 2 de 2 entradas", form.lblResumen.Text);
        });
    }

    [Fact]
    public void Filtro_SoloErrores_FiltraCorrectamente()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Msg info", ""),
                    new(2, "10:01:00", NivelLog.Advertencia, "Msg adv", ""),
                    new(3, "10:02:00", NivelLog.Error, "Msg err", "Error grave"),
                    new(4, "10:03:00", NivelLog.Fatal, "Msg fatal", "Crash")
                }, false, 500)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(4, form.dgvEntradas.Rows.Count);

            // Cambiar a "Solo errores" (índice 3)
            form.cmbNivel.SelectedIndex = 3;

            Assert.Equal(2, form.dgvEntradas.Rows.Count);
            Assert.Contains("Mostrando 2 de 4 entradas", form.lblResumen.Text);
        });
    }

    [Fact]
    public void Filtro_PorTextoEnDetalle_EncuentraEntrada()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Mensaje comun", "Detalle simple"),
                    new(2, "10:01:00", NivelLog.Informacion, "Otro mensaje", "Stack trace: NullReferenceException at Class.Method")
                }, false, 500)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.txtBuscar.Text = "NullReference";
            form.AplicarFiltroParaTest();

            Assert.Single(form.dgvEntradas.Rows);
            Assert.Equal("Otro mensaje", form.dgvEntradas.Rows[0].Cells["colMensaje"].Value);
        });
    }

    [Fact]
    public void Resumen_ConYSinTruncado_MuestraTextoCorrecto()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Info", "")
                }, true, 5000000)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("solo se leen los últimos 4 MB", form.lblResumen.Text);
        });
    }

    [Fact]
    public void SeleccionarFila_LlenaTxtDetalle()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Fila 1", "Detalle 1"),
                    new(2, "10:01:00", NivelLog.Error, "Fila 2", "Traza detallada de error")
                }, false, 500)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.SeleccionarEntradaParaTest(1);

            Assert.Contains("Fila 2", form.txtDetalle.Text);
            Assert.Contains("Traza detallada de error", form.txtDetalle.Text);
        });
    }

    [Fact]
    public void SinArchivos_MuestraMensajeSinArchivosYGrillaVacia()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog>()
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(form.cmbArchivo.Items);
            Assert.Empty(form.dgvEntradas.Rows);
            Assert.Empty(form.txtDetalle.Text);
            Assert.Equal("No hay archivos de log.", form.lblResumen.Text);
        });
    }

    [Fact]
    public void LeerAsync_ConError_MuestraAdvertencia()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("pesaje.log", null, 100) },
                ErrorLeer = new ReglaNegocioException("LOG_ERROR", "Error leyendo el archivo de log.")
            };

            using var form = new FrmVisorLogs(servicio);
            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = m => advertencia = m;

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("Error leyendo el archivo de log.", advertencia);
        });
    }

    [Fact]
    public void TeclaF5_VuelveAListarArchivos()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("pesaje.log", null, 100) }
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(1, servicio.ConteoListar);

            var procesada = form.ProcesarTeclaParaTest(Keys.F5);
            Assert.True(procesada);

            // Regla 0.10: el F5 no debe envolver en EjecutarAsync un método que ya lo usa (se descartaba).
            Assert.Equal(2, servicio.ConteoListar);
            Assert.Single(form.cmbArchivo.Items);
        });
    }

    [Fact]
    public void Detalle_MultilineaSeMuestraConSaltosDeLineaDeWindows()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Error, "Fallo", "System.Exception: x\n   at A()\n   at B()")
                }, false, 500)
            };

            using var form = new FrmVisorLogs(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new[] { "Fallo", "", "System.Exception: x", "   at A()", "   at B()" },
                form.txtDetalle.Text.Split("\r\n"));
        });
    }

    [Fact]
    public void Grilla_NoEsOrdenable_ElDetalleSigueLaFilaSeleccionada()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("test.log", null, 500) },
                Contenido = new ContenidoLog("test.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Msg", "")
                }, false, 500)
            };

            using var form = new FrmVisorLogs(servicio);

            Assert.All(form.dgvEntradas.Columns.Cast<DataGridViewColumn>(),
                c => Assert.Equal(DataGridViewColumnSortMode.NotSortable, c.SortMode));
        });
    }

    [Fact]
    public void CambiarDeArchivo_ConErrorDeLectura_NoDejaElContenidoDelArchivoAnterior()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso
            {
                Archivos = new List<ArchivoLog> { new("a.log", null, 100), new("b.log", null, 100) },
                Contenido = new ContenidoLog("a.log", new List<EntradaLog>
                {
                    new(1, "10:00:00", NivelLog.Informacion, "Del archivo A", "")
                }, false, 100)
            };

            using var form = new FrmVisorLogs(servicio);
            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = m => advertencia = m;
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            Assert.Single(form.dgvEntradas.Rows);

            servicio.ErrorLeer = new ReglaNegocioException("LOG_ERROR", "No se pudo leer b.log.");
            form.SeleccionarArchivoParaTestAsync(1).GetAwaiter().GetResult();

            Assert.Equal("No se pudo leer b.log.", advertencia);
            Assert.Empty(form.dgvEntradas.Rows);
        });
    }

    [Fact]
    public void AbrirCarpeta_InvocaGanchoConRutaDeLogs()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioLogsFalso { CarpetaLogs = @"D:\LogsSistema" };
            using var form = new FrmVisorLogs(servicio);

            string? rutaRecibida = null;
            form.AbrirCarpetaParaTest = r => rutaRecibida = r;

            form.PulsarAbrirCarpetaParaTest();

            Assert.Equal(@"D:\LogsSistema", rutaRecibida);
        });
    }
}
