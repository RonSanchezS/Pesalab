namespace PesajeFaeneo.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Backup;
using Xunit;

public class ServicioBackupTests
{
    [Fact]
    public async Task Test01_MetodosConPermiso_SinPermiso_LanzanNoAutorizadoExceptionYSinLlamadasAlMotor()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso();
        var programador = new ProgramadorBackupFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioBackup(
            motor,
            almacen,
            programador,
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioBackup>.Instance);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ExportarAsync("a.zip", "b.zip"));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ValidarAsync("a.zip"));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.DescartarValidacionAsync("tok"));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerConfiguracionAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearManualAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ProgramarRestauracionAsync("tok", "RESTAURAR"));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.GuardarConfiguracionAsync(ConfiguracionBackup.PorDefecto));

        Assert.Empty(motor.Solicitudes);
        Assert.Empty(motor.Exportaciones);
        Assert.Empty(motor.Validaciones);
        Assert.Empty(motor.Programaciones);
        Assert.Empty(motor.Descartes);
        Assert.Empty(motor.CarpetasVerificadas);
    }

    [Fact]
    public async Task Test02_CrearManualAsync_ConPermiso_CreaBackupYRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso
        {
            Configuracion = new ConfiguracionBackup(true, true, 45, @"D:\CopiaSecundaria")
        };
        var programador = new ProgramadorBackupFalso();
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        var servicio = new ServicioBackup(
            motor,
            almacen,
            programador,
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioBackup>.Instance);

        var resultado = await servicio.CrearManualAsync();

        Assert.NotNull(resultado.Backup);
        var sol = Assert.Single(motor.Solicitudes);
        Assert.Equal(TipoBackup.Manual, sol.Tipo);
        Assert.Equal(45, sol.DiasRetencion);
        Assert.Equal(@"D:\CopiaSecundaria", sol.CarpetaSecundaria);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditoria = await ctx.Auditorias.SingleAsync();
        Assert.Equal(AccionAuditoria.Backup, auditoria.Accion);
        Assert.Equal(usuario.Id, auditoria.UsuarioId);
        Assert.Equal(EntidadesAuditoria.Sistema, auditoria.Entidad);
        Assert.Equal("Backup", auditoria.EntidadId);
        Assert.Contains(resultado.Backup.NombreArchivo, auditoria.DatosDespuesJson);
        Assert.Contains("Manual", auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task Test02b_CrearManualAsync_SiLaAuditoriaFalla_DevuelveLaCopiaYaCreada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var motor = new MotorBackupFalso();
        var servicio = new ServicioBackup(
            motor,
            new AlmacenConfiguracionBackupFalso(),
            new ProgramadorBackupFalso(),
            new FabricaUowQueFalla(),
            sesion,
            new FakeTimeProvider(),
            NullLogger<ServicioBackup>.Instance);

        var resultado = await servicio.CrearManualAsync();

        Assert.NotNull(resultado.Backup);
        Assert.Single(motor.Solicitudes);
    }

    private sealed class FabricaUowQueFalla : PesajeFaeneo.Application.Abstracciones.Persistencia.IUnidadDeTrabajoFactory
    {
        public Task<PesajeFaeneo.Application.Abstracciones.Persistencia.IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Base ocupada (simulado).");
    }

    [Fact]
    public async Task Test03_CrearManualAsync_SiMotorLanza_NoRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var motor = new MotorBackupFalso
        {
            ErrorCrear = new ReglaNegocioException("BAK_ERROR", "Falla simulada")
        };
        var almacen = new AlmacenConfiguracionBackupFalso();
        var programador = new ProgramadorBackupFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioBackup(
            motor,
            almacen,
            programador,
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioBackup>.Instance);

        await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearManualAsync());

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await ctx.Auditorias.ToListAsync());
    }

    [Fact]
    public async Task Test04_ProgramarRestauracionAsync_ValidaConfirmacionYRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador, "admin_user");

        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso();
        var programador = new ProgramadorBackupFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioBackup(
            motor,
            almacen,
            programador,
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioBackup>.Instance);

        // Confirmaciones incorrectas
        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ProgramarRestauracionAsync("tok1", "restaurar"));
        Assert.Equal(CamposBackup.Confirmacion, ex1.Errores[0].Campo);

        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ProgramarRestauracionAsync("tok1", ""));
        Assert.Equal(CamposBackup.Confirmacion, ex2.Errores[0].Campo);

        var ex3 = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ProgramarRestauracionAsync("tok1", null!));
        Assert.Equal(CamposBackup.Confirmacion, ex3.Errores[0].Campo);

        Assert.Empty(motor.Programaciones);

        // Confirmación válida con espacios
        var resguardo = await servicio.ProgramarRestauracionAsync("tok_valido", "  RESTAURAR ");

        Assert.NotNull(resguardo);
        var prog = Assert.Single(motor.Programaciones);
        Assert.Equal("tok_valido", prog.Token);
        Assert.Equal("admin_user", prog.SolicitadoPor);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditoria = await ctx.Auditorias.SingleAsync();
        Assert.Equal(AccionAuditoria.Restauracion, auditoria.Accion);
        Assert.Equal(usuario.Id, auditoria.UsuarioId);
        Assert.Equal(EntidadesAuditoria.Sistema, auditoria.Entidad);
        Assert.Equal("Restauracion", auditoria.EntidadId);
        Assert.Contains(resguardo.NombreArchivo, auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task Test05_GuardarConfiguracionAsync_ValidaCarpetasYAuditaSoloSiHayCambios()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso
        {
            Configuracion = new ConfiguracionBackup(true, true, 30, null)
        };
        var programador = new ProgramadorBackupFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioBackup(
            motor,
            almacen,
            programador,
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioBackup>.Instance);

        // 1. 5 días -> error DiasRetencion
        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.GuardarConfiguracionAsync(new ConfiguracionBackup(true, true, 5, null)));
        Assert.Contains(ex1.Errores, e => e.Campo == CamposBackup.DiasRetencion);

        // 2. Carpeta relativa "Backups" -> error CarpetaSecundaria
        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.GuardarConfiguracionAsync(new ConfiguracionBackup(true, true, 30, "Backups")));
        Assert.Contains(ex2.Errores, e => e.Campo == CamposBackup.CarpetaSecundaria);

        // 3. Carpeta con solo espacios -> se normaliza a null y no verifica carpeta
        await servicio.GuardarConfiguracionAsync(new ConfiguracionBackup(true, true, 30, "   "));
        Assert.Empty(motor.CarpetasVerificadas);

        // 4. Carpeta válida -> motor verifica la carpeta
        await servicio.GuardarConfiguracionAsync(new ConfiguracionBackup(true, true, 60, @"D:\Secundaria"));
        Assert.Contains(@"D:\Secundaria", motor.CarpetasVerificadas);
        Assert.Equal(1, almacen.Guardados);

        // 5. Guardar exactamente la misma configuración -> no guarda ni audita nuevamente
        await servicio.GuardarConfiguracionAsync(new ConfiguracionBackup(true, true, 60, @"D:\Secundaria"));
        Assert.Equal(1, almacen.Guardados);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditorias = await ctx.Auditorias.ToListAsync();
        Assert.Single(auditorias);
        Assert.Equal(AccionAuditoria.CambioParametros, auditorias[0].Accion);
        Assert.Equal(EntidadesAuditoria.Sistema, auditorias[0].Entidad);
        Assert.Equal("ConfiguracionBackup", auditorias[0].EntidadId);
    }
}
