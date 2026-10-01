using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Importacion;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmImportarLegadoTests
{
    private readonly FakeTimeProvider _tiempo = new(new DateTimeOffset(2026, 10, 1, 10, 30, 0, TimeSpan.Zero));

    [Fact]
    public void EstadoInicial_BotonesYControlesConfigurados()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            using var form = new FrmImportarLegado(servicio, _tiempo);

            Assert.False(form.btnSimular.Enabled);
            Assert.False(form.btnImportar.Enabled);
            Assert.False(form.btnInforme.Enabled);
            Assert.False(form.btnLeer.Enabled);
            Assert.False(form.cmbTabla.Enabled);
            Assert.True(form.cmbCodificacion.Enabled);
            Assert.Equal("Ganaderos", form.cmbTipo.Text);
        });
    }

    [Fact]
    public void Examinar_SegunExtension_ConfiguraTablaYCodificacion_YManejaErrores()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            using var form = new FrmImportarLegado(servicio, _tiempo);
            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = m => advertencia = m;

            // 1. Archivo .csv
            form.ElegirArchivoParaTest = () => @"C:\Datos\ganaderos.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            Assert.True(form.cmbCodificacion.Enabled);
            Assert.False(form.cmbTabla.Enabled);
            Assert.True(form.btnLeer.Enabled);

            // 2. Archivo .mdb con tablas
            servicio.Tablas = new List<string> { "TABLA1", "TABLA2" };
            form.ElegirArchivoParaTest = () => @"C:\Datos\legado.mdb";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            Assert.False(form.cmbCodificacion.Enabled);
            Assert.True(form.cmbTabla.Enabled);
            Assert.Equal(2, form.cmbTabla.Items.Count);
            Assert.Equal("TABLA1", form.cmbTabla.Text);

            // 3. Error en listar tablas de Access
            servicio.ErrorListarTablas = new ReglaNegocioException("IMP_ACCESS_SIN_MOTOR", "No se detectó el motor ACE.");
            form.ElegirArchivoParaTest = () => @"C:\Datos\otro.accdb";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            Assert.Contains("No se detectó el motor ACE.", advertencia);
            servicio.ErrorListarTablas = null;

            // 4. Archivo no soportado
            advertencia = null;
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.xls";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            Assert.Contains("Tipo de archivo no soportado", advertencia);
        });
    }

    [Fact]
    public void Leer_ConfiguraSolicitud_LlenaMapeoYMuestra_YMuestraAdvertencias()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            var columnas = new List<string> { "COD", "NOM", "EXTRA" };
            var muestra = new List<FilaOrigen>
            {
                new(2, new List<string?> { "G01", "JUAN PEREZ", "X" }),
                new(3, new List<string?> { "G02", "MARIA LOPEZ", "Y" })
            };
            var sugerido = new Dictionary<string, int?>
            {
                ["Marca"] = 0,
                ["Nombre"] = 1
            };
            servicio.Vista = new VistaOrigen(columnas, muestra, 150, "DOS 850", new[] { "Advertencia 1" }, sugerido);

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\ganaderos.dbf";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();

            // Seleccionar codificación "DOS 850"
            form.cmbCodificacion.SelectedIndex = 3; // "DOS 850"

            form.LeerParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.LecturasSolicitadas);
            var req = servicio.LecturasSolicitadas[0];
            Assert.Equal(FormatoOrigen.Dbf, req.Lectura.Formato);
            Assert.Equal(850, req.Lectura.PaginaCodigos);
            Assert.Equal(TipoImportacion.Ganaderos, req.Tipo);

            // Verificar dgvMapeo
            Assert.True(form.dgvMapeo.Rows.Count >= 2);
            var mapeo = form.MapeoActualParaTest();
            Assert.Equal(0, mapeo["Marca"]);
            Assert.Equal(1, mapeo["Nombre"]);

            // Verificar dgvMuestra
            Assert.Equal(3, form.dgvMuestra.Columns.Count);
            Assert.Equal(2, form.dgvMuestra.Rows.Count);

            // Verificar info archivo
            Assert.Contains("150 filas", form.lblInfoArchivo.Text);
            Assert.Contains("Advertencia 1", form.lblInfoArchivo.Text);

            Assert.True(form.btnSimular.Enabled);
        });
    }

    [Fact]
    public void CambiarTipoTrasLeer_LimpiaMapeoYMuestra_YDeshabilitaSimular()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "COL1" },
                new List<FilaOrigen> { new(1, new List<string?> { "V1" }) },
                10,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?>());

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.btnSimular.Enabled);
            Assert.True(form.dgvMapeo.Rows.Count > 0);

            // Cambiar tipo
            form.cmbTipo.SelectedIndex = 1; // Clientes

            Assert.False(form.btnSimular.Enabled);
            Assert.Empty(form.dgvMapeo.Rows);
            Assert.Empty(form.dgvMuestra.Rows);
        });
    }

    [Fact]
    public void Simular_EnviaMapeoDeGrilla_LlenaResumenEIncidencias_YHabilitaImportar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "COD", "NOM" },
                new List<FilaOrigen>(),
                20,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?> { ["Marca"] = 0, ["Nombre"] = 1 });

            var incidencias = new List<Incidencia>
            {
                new(5, "Marca", NivelIncidencia.Advertencia, "Espacios recortados"),
                new(8, "Nombre", NivelIncidencia.Error, "Nombre requerido")
            };
            servicio.ResultadoSimulacro = new ResultadoImportacion(
                true, "HUELLA-SIM", TipoImportacion.Ganaderos, 20, 18, 0, 1, 1, 1, incidencias, false, null);

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();

            // Cambiar combo de Nombre a COD para probar mapeo modificado
            form.dgvMapeo.Rows[1].Cells["colColumna"].Value = "COD";

            form.SimularParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.SimulacionesSolicitadas);
            var sol = servicio.SimulacionesSolicitadas[0];
            Assert.Equal(0, sol.Mapeo["Marca"]);
            Assert.Equal(0, sol.Mapeo["Nombre"]); // ambos apuntan a 0 (COD)

            Assert.Contains("se importarían 18", form.lblResumen.Text);
            Assert.Contains("1 con errores", form.lblResumen.Text);
            Assert.Equal(2, form.dgvIncidencias.Rows.Count);
            Assert.True(form.btnImportar.Enabled);
        });
    }

    [Fact]
    public void CambiarMapeoDespuesDeSimular_DeshabilitaImportar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "A", "B" },
                new List<FilaOrigen>(),
                10,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?> { ["Marca"] = 0, ["Nombre"] = 1 });
            servicio.ResultadoSimulacro = new ResultadoImportacion(
                true, "HUELLA-SIM", TipoImportacion.Ganaderos, 10, 10, 0, 0, 0, 0, Array.Empty<Incidencia>(), false, null);

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();
            form.SimularParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.btnImportar.Enabled);

            // Cambiar celda
            form.dgvMapeo.Rows[0].Cells["colColumna"].Value = "B";

            Assert.False(form.btnImportar.Enabled);
            Assert.Contains("El mapeo cambió", form.lblResumen.Text);
        });
    }

    [Fact]
    public void Importar_RespetaConfirmacion_YActualizaEstado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "A", "B" },
                new List<FilaOrigen>(),
                10,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?> { ["Marca"] = 0, ["Nombre"] = 1 });
            servicio.ResultadoSimulacro = new ResultadoImportacion(
                true, "HUELLA-SIM", TipoImportacion.Ganaderos, 10, 8, 0, 1, 1, 0, Array.Empty<Incidencia>(), false, null);
            servicio.ResultadoImportacion = new ResultadoImportacion(
                false, "HUELLA-SIM", TipoImportacion.Ganaderos, 10, 8, 8, 1, 1, 0, Array.Empty<Incidencia>(), false, "pre-backup.zip");

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();
            form.SimularParaTestAsync().GetAwaiter().GetResult();

            // 1. Confirmar = false
            form.ConfirmarImportacionParaTest = _ => false;
            form.ImportarParaTestAsync().GetAwaiter().GetResult();
            Assert.Empty(servicio.ImportacionesSolicitadas);
            Assert.True(form.btnImportar.Enabled);

            // 2. Confirmar = true
            string? infoMostrada = null;
            form.MostrarInformacionParaTest = m => infoMostrada = m;
            form.ConfirmarImportacionParaTest = _ => true;
            form.ImportarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.ImportacionesSolicitadas);
            Assert.Equal("HUELLA-SIM", servicio.ImportacionesSolicitadas[0].Huella);
            Assert.False(form.btnImportar.Enabled);
            Assert.True(form.btnInforme.Enabled);
            Assert.NotNull(infoMostrada);
            Assert.Contains("8 fila(s) importada(s)", infoMostrada);
            Assert.Contains("pre-backup.zip", infoMostrada);
        });
    }

    [Fact]
    public void Importar_ConErrorCambio_MuestraAdvertenciaYDeshabilitaImportar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "A", "B" },
                new List<FilaOrigen>(),
                10,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?> { ["Marca"] = 0, ["Nombre"] = 1 });
            servicio.ResultadoSimulacro = new ResultadoImportacion(
                true, "HUELLA-VIEJA", TipoImportacion.Ganaderos, 10, 8, 0, 1, 1, 0, Array.Empty<Incidencia>(), false, null);

            servicio.ErrorImportar = new ReglaNegocioException("IMP_CAMBIO", "El archivo cambió tras el simulacro.");

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();
            form.SimularParaTestAsync().GetAwaiter().GetResult();

            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = m => advertencia = m;
            form.ConfirmarImportacionParaTest = _ => true;

            form.ImportarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("El archivo cambió tras el simulacro.", advertencia);
            Assert.False(form.btnImportar.Enabled);
        });
    }

    [Fact]
    public void Simular_ConValidacionException_MuestraUnaAdvertenciaConTodosLosMensajes()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "A" },
                new List<FilaOrigen>(),
                5,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?>());

            servicio.ErrorSimular = new ValidacionException(new[]
            {
                new ErrorValidacion("Marca", "Falta mapear la Marca."),
                new ErrorValidacion("Nombre", "Falta mapear el Nombre.")
            });

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\archivo.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();

            string? advertencia = null;
            form.MostrarAdvertenciaParaTest = m => advertencia = m;

            form.SimularParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(advertencia);
            Assert.Contains("Falta mapear la Marca.", advertencia);
            Assert.Contains("Falta mapear el Nombre.", advertencia);
            Assert.Contains("\n", advertencia);
        });
    }

    [Fact]
    public void GuardarInforme_GeneraCsvConBom_YSiRutaEsNullNoEscribe()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioImportacionFalso();
            servicio.Vista = new VistaOrigen(
                new[] { "A", "B" },
                new List<FilaOrigen>(),
                10,
                "Windows-1252",
                Array.Empty<string>(),
                new Dictionary<string, int?> { ["Marca"] = 0, ["Nombre"] = 1 });

            var incidencias = new List<Incidencia>
            {
                new(2, "Marca", NivelIncidencia.Error, "Marca repetida")
            };
            servicio.ResultadoSimulacro = new ResultadoImportacion(
                true, "HUELLA", TipoImportacion.Ganaderos, 10, 9, 0, 0, 1, 0, incidencias, false, null);

            using var form = new FrmImportarLegado(servicio, _tiempo);
            form.ElegirArchivoParaTest = () => @"C:\Datos\ganaderos.csv";
            form.ExaminarParaTestAsync().GetAwaiter().GetResult();
            form.LeerParaTestAsync().GetAwaiter().GetResult();
            form.SimularParaTestAsync().GetAwaiter().GetResult();

            var tempDir = Path.Combine(Path.GetTempPath(), "PesalabTestInforme_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                var rutaInforme = Path.Combine(tempDir, "informe.csv");
                form.ElegirRutaInformeParaTest = sugerido =>
                {
                    Assert.Contains("Importacion_Ganaderos_20261001_1030.csv", sugerido);
                    return rutaInforme;
                };

                form.GuardarInformeParaTest();

                Assert.True(File.Exists(rutaInforme));
                var bytes = File.ReadAllBytes(rutaInforme);
                // Verificar BOM UTF-8 (EF BB BF)
                Assert.Equal(0xEF, bytes[0]);
                Assert.Equal(0xBB, bytes[1]);
                Assert.Equal(0xBF, bytes[2]);

                var contenido = Encoding.UTF8.GetString(bytes);
                Assert.Contains("Fila;Nivel;Campo;Mensaje", contenido);
                Assert.Contains("Marca repetida", contenido);

                // Si ruta es null, no debe fallar
                form.ElegirRutaInformeParaTest = _ => null;
                form.GuardarInformeParaTest();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        });
    }
}
