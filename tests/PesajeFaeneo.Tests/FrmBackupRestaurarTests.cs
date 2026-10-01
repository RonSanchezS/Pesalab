using System.Windows.Forms;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Backup;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmBackupRestaurarTests
{
    private static FakeTimeProvider Reloj()
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));
        return tiempo;
    }

    [Fact]
    public void Test01_Carga_FilasYTamanoYEstadoHabilitacion()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1572864, // 1.5 MB
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);

            var b2 = new InfoBackup(
                @"C:\FakeBackups\manuales\b2.zip",
                "b2.zip",
                TipoBackup.Automatico,
                DateTime.UtcNow,
                2048,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                0,
                null,
                Valido: false,
                Problema: "Archivo dañado");

            servicio.Lista.Add(b1);
            servicio.Lista.Add(b2);

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(2, form.dgvBackups.Rows.Count);
            Assert.Equal("1,5 MB", form.dgvBackups.Rows[0].Cells["colTamano"].Value);
            Assert.Equal("Correcta", form.dgvBackups.Rows[0].Cells["colEstado"].Value);
            Assert.Equal("Manual", form.dgvBackups.Rows[0].Cells["colTipo"].Value);

            Assert.Equal("Archivo dañado", form.dgvBackups.Rows[1].Cells["colEstado"].Value);
            Assert.Equal("Automático", form.dgvBackups.Rows[1].Cells["colTipo"].Value);

            // Seleccionar fila 0 (válida)
            form.SeleccionarFilaParaTest(0);
            Assert.True(form.btnExportar.Enabled);
            Assert.True(form.btnRestaurar.Enabled);

            // Seleccionar fila 1 (inválida)
            form.SeleccionarFilaParaTest(1);
            Assert.False(form.btnExportar.Enabled);
            Assert.False(form.btnRestaurar.Enabled);
        });
    }

    [Fact]
    public void Test02_Configuracion_CargaControlesYEstadosUltimoAutomatico()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso
            {
                Configuracion = new ConfiguracionBackup(true, true, 45, @"D:\BackupsSecundarios")
            };

            // Variante 1: null
            servicio.UltimoAutomatico = null;
            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.chkAutomatico.Checked);
            Assert.True(form.chkAlCerrar.Checked);
            Assert.Equal(45, form.nudDias.Value);
            Assert.Equal(@"D:\BackupsSecundarios", form.txtCarpetaSecundaria.Text);
            Assert.Equal("Sin backups automáticos en esta sesión.", form.lblUltimoAutomatico.Text);

            // Variante 2: exitoso
            servicio.UltimoAutomatico = new ResultadoBackupAutomatico(
                new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
                Exito: true,
                NombreArchivo: "PesajeFaeneo_automatico_2026-10-01.zip",
                Mensaje: null);

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            Assert.Contains("Último backup automático:", form.lblUltimoAutomatico.Text);
            Assert.Contains("PesajeFaeneo_automatico_2026-10-01.zip", form.lblUltimoAutomatico.Text);

            // Variante 3: fallido
            servicio.UltimoAutomatico = new ResultadoBackupAutomatico(
                new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
                Exito: false,
                NombreArchivo: null,
                Mensaje: "Disco lleno");

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            Assert.Contains("FALLÓ", form.lblUltimoAutomatico.Text);
            Assert.Contains("Disco lleno", form.lblUltimoAutomatico.Text);
        });
    }

    [Fact]
    public void Test03_Crear_ExitoYFallo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            string? mensajeInfo = null;
            string? mensajeAdv = null;

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.MostrarInformacionParaTest = m => mensajeInfo = m;
            form.MostrarAdvertenciaParaTest = m => mensajeAdv = m;

            // Éxito
            form.CrearParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(1, servicio.ConteoCrearManual);
            Assert.NotNull(mensajeInfo);
            Assert.Contains("Copia creada: PesajeFaeneo_manual_test.zip", mensajeInfo);
            Assert.Null(mensajeAdv);

            // Fallo con ReglaNegocioException
            mensajeInfo = null;
            mensajeAdv = null;
            servicio.ErrorCrearManual = new ReglaNegocioException("BAK_ERROR", "Error simulado");
            form.CrearParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(2, servicio.ConteoCrearManual);
            Assert.Null(mensajeInfo);
            Assert.NotNull(mensajeAdv);
            Assert.Contains("Error simulado", mensajeAdv);
        });
    }

    [Fact]
    public void Test04_Exportar_ExitoYCancelado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);
            servicio.Lista.Add(b1);

            string? mensajeInfo = null;
            string? mensajeAdv = null;

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.MostrarInformacionParaTest = m => mensajeInfo = m;
            form.MostrarAdvertenciaParaTest = m => mensajeAdv = m;
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(0);

            // Destino null (cancela)
            form.ElegirDestinoExportarParaTest = _ => null;
            form.ExportarParaTestAsync().GetAwaiter().GetResult();
            Assert.Empty(servicio.Exportaciones);

            // Destino con ruta válida
            form.ElegirDestinoExportarParaTest = _ => @"E:\Export\copia.zip";
            form.ExportarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Exportaciones);
            Assert.Equal(@"C:\FakeBackups\manuales\b1.zip", servicio.Exportaciones[0].RutaBackup);
            Assert.Equal(@"E:\Export\copia.zip", servicio.Exportaciones[0].RutaDestino);
            Assert.NotNull(mensajeInfo);
            Assert.Contains("copia.zip", mensajeInfo);

            // Error en exportar
            mensajeInfo = null;
            servicio.ErrorExportar = new ReglaNegocioException("EXP_ERR", "Fallo al exportar");
            form.ExportarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotNull(mensajeAdv);
            Assert.Contains("Fallo al exportar", mensajeAdv);
        });
    }

    [Fact]
    public void Test05_Restaurar_ValidarAsyncLanza_MuestraAdvertenciaYSinConfirmar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);
            servicio.Lista.Add(b1);

            string? mensajeAdv = null;
            bool dialogoConfirmacionInvocado = false;

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.MostrarAdvertenciaParaTest = m => mensajeAdv = m;
            form.ConfirmarRestauracionParaTest = _ =>
            {
                dialogoConfirmacionInvocado = true;
                return null;
            };

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(0);

            servicio.ErrorValidar = new ValidacionException("Archivo", "Archivo dañado");
            form.RestaurarSeleccionadaParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(mensajeAdv);
            Assert.Contains("Archivo dañado", mensajeAdv);
            Assert.False(dialogoConfirmacionInvocado);
        });
    }

    [Fact]
    public void Test06_Restaurar_ConfirmacionNull_DescartaValidacionYSinReiniciar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);
            servicio.Lista.Add(b1);

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.ConfirmarRestauracionParaTest = _ => null; // Cancelado en confirmación

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(0);

            form.RestaurarSeleccionadaParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Descartes);
            Assert.Empty(servicio.Programaciones);
            Assert.False(form.ReinicioSolicitado);
        });
    }

    [Fact]
    public void Test07_Restaurar_ConfirmacionRESTAURAR_ProgramaCopiaYSolicitaReinicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);
            servicio.Lista.Add(b1);

            string? mensajeInfo = null;

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.MostrarInformacionParaTest = m => mensajeInfo = m;
            form.ConfirmarRestauracionParaTest = _ => "RESTAURAR";

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(0);

            form.RestaurarSeleccionadaParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Programaciones);
            Assert.Equal("RESTAURAR", servicio.Programaciones[0].Confirmacion);
            Assert.True(form.ReinicioSolicitado);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.NotNull(mensajeInfo);
            Assert.Contains("restauración quedó programada", mensajeInfo);
        });
    }

    [Fact]
    public void Test08_Restaurar_ProgramarLanza_MuestraAdvertenciaYSinReinicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            var b1 = new InfoBackup(
                @"C:\FakeBackups\manuales\b1.zip",
                "b1.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null);
            servicio.Lista.Add(b1);

            string? mensajeAdv = null;
            servicio.ErrorProgramar = new ReglaNegocioException("BAK_SIN_RESGUARDO", "No se pudo crear el resguardo");

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.MostrarAdvertenciaParaTest = m => mensajeAdv = m;
            form.ConfirmarRestauracionParaTest = _ => "RESTAURAR";

            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(0);

            form.RestaurarSeleccionadaParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(mensajeAdv);
            Assert.Contains("No se pudo crear el resguardo", mensajeAdv);
            Assert.False(form.ReinicioSolicitado);
        });
    }

    [Fact]
    public void Test09_RestaurarDesdeArchivo_ElegirArchivo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            using var form = new FrmBackupRestaurar(servicio, Reloj());

            // Null: no hace nada
            form.ElegirArchivoBackupParaTest = () => null;
            form.RestaurarDesdeArchivoParaTestAsync().GetAwaiter().GetResult();
            Assert.Empty(servicio.Validaciones);

            // Con ruta: valida esa ruta
            form.ElegirArchivoBackupParaTest = () => @"C:\Otro\archivo.zip";
            form.ConfirmarRestauracionParaTest = _ => null; // cancela después
            form.RestaurarDesdeArchivoParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Validaciones);
            Assert.Equal(@"C:\Otro\archivo.zip", servicio.Validaciones[0]);
        });
    }

    [Fact]
    public void Test10_GuardarConfiguracion_ValidacionYValores()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.nudDias.Value = 60;
            form.chkAutomatico.Checked = true;
            form.chkAlCerrar.Checked = false;

            // Examinar carpeta
            form.ElegirCarpetaParaTest = () => @"F:\Secundaria";
            form.ExaminarCarpetaParaTest();
            Assert.Equal(@"F:\Secundaria", form.txtCarpetaSecundaria.Text);

            // Guardar
            form.GuardarConfiguracionParaTestAsync().GetAwaiter().GetResult();
            Assert.Single(servicio.ConfiguracionesGuardadas);
            Assert.Equal(60, servicio.ConfiguracionesGuardadas[0].DiasRetencion);
            Assert.Equal(@"F:\Secundaria", servicio.ConfiguracionesGuardadas[0].CarpetaSecundaria);
            Assert.Equal("Configuración guardada.", form.lblEstado.Text);

            // Quitar carpeta
            form.QuitarCarpetaParaTest();
            Assert.Equal("", form.txtCarpetaSecundaria.Text);
            form.GuardarConfiguracionParaTestAsync().GetAwaiter().GetResult();
            Assert.Null(servicio.ConfiguracionesGuardadas[1].CarpetaSecundaria);

            // ValidacionException en DiasRetencion
            servicio.ErrorGuardarConfiguracion = new ValidacionException(CamposBackup.DiasRetencion, "Indique entre 7 y 365 días.");
            form.GuardarConfiguracionParaTestAsync().GetAwaiter().GetResult();
            // Control errorProvider no vacío
            var err = form.ErrorDe(form.nudDias);
            Assert.Contains("Indique entre 7 y 365 días.", err);
        });
    }

    [Fact]
    public void Test11_AbrirCarpeta_Y_TeclaF5Recarga()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioBackupFalso();
            string? carpetaAbierta = null;

            using var form = new FrmBackupRestaurar(servicio, Reloj());
            form.AbrirCarpetaParaTest = c => carpetaAbierta = c;

            form.PulsarAbrirCarpetaParaTest();
            Assert.Equal(servicio.CarpetaBackups, carpetaAbierta);

            // F5 recarga la grilla
            var conteoPrevio = servicio.ConteoListar;
            form.ProcesarTeclaParaTest(Keys.F5);
            Assert.Equal(conteoPrevio + 1, servicio.ConteoListar);
        });
    }
}
