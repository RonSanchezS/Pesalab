namespace PesajeFaeneo.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Arranque;
using PesajeFaeneo.Application.Arranque.Dtos;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

public sealed class ActualizadorBaseDatosTests
{
    private sealed class InicializadorFalso : IDatabaseInitializer
    {
        public EstadoEsquemaBaseDatos Estado { get; set; } = new([], [], []);
        public Exception? ExcepcionInicializar { get; set; }
        public List<string> Llamadas { get; }

        public InicializadorFalso(List<string> llamadas)
        {
            Llamadas = llamadas;
        }

        public Task<EstadoEsquemaBaseDatos> EvaluarAsync(CancellationToken cancellationToken = default)
        {
            Llamadas.Add("Evaluar");
            return Task.FromResult(Estado);
        }

        public Task InicializarAsync(CancellationToken cancellationToken = default)
        {
            Llamadas.Add("Inicializar");
            if (ExcepcionInicializar != null)
            {
                throw ExcepcionInicializar;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class MotorBackupFalso : IMotorBackup
    {
        public Exception? ExcepcionCrear { get; set; }
        public SolicitudBackup? UltimaSolicitud { get; private set; }
        public List<string> Llamadas { get; }
        public string NombreArchivoGenerado { get; set; } = "PesajeFaeneo_premigracion_2026-10-03_100000.zip";

        public MotorBackupFalso(List<string> llamadas)
        {
            Llamadas = llamadas;
        }

        public string CarpetaBackups => "C:\\Backups";

        public Task<ResultadoCreacionBackup> CrearAsync(SolicitudBackup solicitud, CancellationToken cancellationToken = default)
        {
            Llamadas.Add("Backup");
            UltimaSolicitud = solicitud;
            if (ExcepcionCrear != null)
            {
                throw ExcepcionCrear;
            }
            var info = new InfoBackup(
                "C:\\Backups\\seguridad\\" + NombreArchivoGenerado,
                NombreArchivoGenerado,
                solicitud.Tipo,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO",
                "mig1",
                0,
                null,
                true,
                null);
            return Task.FromResult(new ResultadoCreacionBackup(info, null));
        }

        public Task<bool> ExisteAutomaticoDelDiaAsync(DateOnly fechaLocal, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task VerificarCarpetaEscribibleAsync(string carpeta, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ValidacionRestauracion> ValidarParaRestaurarAsync(string rutaBackup, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<InfoBackup> ProgramarRestauracionAsync(string token, string solicitadoPor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FabricaUowFalsa : IUnidadDeTrabajoFactory
    {
        public Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private readonly List<string> _llamadas = new();
    private readonly InicializadorFalso _inicializador;
    private readonly MotorBackupFalso _motorBackup;
    private readonly FakeTimeProvider _tiempo;
    private readonly ActualizadorBaseDatos _actualizador;

    public ActualizadorBaseDatosTests()
    {
        _inicializador = new InicializadorFalso(_llamadas);
        _motorBackup = new MotorBackupFalso(_llamadas);
        _tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        _actualizador = new ActualizadorBaseDatos(
            _inicializador,
            _motorBackup,
            new FabricaUowFalsa(),
            _tiempo,
            NullLogger<ActualizadorBaseDatos>.Instance);
    }

    [Fact]
    public async Task PrepararAsync_NuevaInstalacion_SinCopia_InicializaUnaVez()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos([], ["mig1", "mig2", "mig3"], []);

        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.NuevaInstalacion, resultado.Estado);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Null(resultado.ArchivoBackup);
        Assert.Equal(["Evaluar", "Inicializar"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_AlDia_SinCopia_InicializaUnaVez()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1", "mig2", "mig3"], [], []);

        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.AlDia, resultado.Estado);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Null(resultado.ArchivoBackup);
        Assert.Equal(["Evaluar", "Inicializar"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_Actualizacion_CopiaAntesDeInicializar_ConTipoPreMigracion()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1", "mig2"], ["mig3"], []);

        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.Actualizada, resultado.Estado);
        Assert.Equal(["mig3"], resultado.MigracionesAplicadas);
        Assert.Equal(_motorBackup.NombreArchivoGenerado, resultado.ArchivoBackup);
        Assert.Equal(["Evaluar", "Backup", "Inicializar"], _llamadas);

        Assert.NotNull(_motorBackup.UltimaSolicitud);
        Assert.Equal(TipoBackup.PreMigracion, _motorBackup.UltimaSolicitud.Tipo);
        Assert.Equal(0, _motorBackup.UltimaSolicitud.DiasRetencion);
        Assert.Null(_motorBackup.UltimaSolicitud.CarpetaSecundaria);
    }

    [Fact]
    public async Task PrepararAsync_Desconocidas_LanzaVersionMasNueva_SoloEvalua()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1"], [], ["29991231000000_Futura"]);

        var ex = await Assert.ThrowsAsync<ArranqueBaseDatosException>(() => _actualizador.PrepararAsync());

        Assert.Equal(MotivoFalloArranque.VersionMasNueva, ex.Motivo);
        Assert.Equal(ActualizadorBaseDatos.MensajeVersionMasNueva, ex.Message);
        Assert.Equal(["Evaluar"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_CopiaLanzaReglaNegocioException_LanzaBackupFallido_NoInicializa()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1"], ["mig2"], []);
        _motorBackup.ExcepcionCrear = new ReglaNegocioException("BAK_ESPACIO", "No hay espacio suficiente en el disco para crear la copia de seguridad.");

        var ex = await Assert.ThrowsAsync<ArranqueBaseDatosException>(() => _actualizador.PrepararAsync());

        Assert.Equal(MotivoFalloArranque.BackupFallido, ex.Motivo);
        Assert.Contains("No hay espacio suficiente en el disco para crear la copia de seguridad.", ex.Message);
        Assert.Equal(["Evaluar", "Backup"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_CopiaLanzaInvalidOperation_OcultaDetalleInterno()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1"], ["mig2"], []);
        _motorBackup.ExcepcionCrear = new InvalidOperationException("secreto");

        var ex = await Assert.ThrowsAsync<ArranqueBaseDatosException>(() => _actualizador.PrepararAsync());

        Assert.Equal(MotivoFalloArranque.BackupFallido, ex.Motivo);
        Assert.Contains("error inesperado (ver el log).", ex.Message);
        Assert.DoesNotContain("secreto", ex.Message);
    }

    [Fact]
    public async Task PrepararAsync_ActualizacionYMigracionFalla_LanzaMigracionFallidaConNombreBackup()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1"], ["mig2"], []);
        var original = new InvalidOperationException("Error de migración SQL");
        _inicializador.ExcepcionInicializar = original;

        var ex = await Assert.ThrowsAsync<ArranqueBaseDatosException>(() => _actualizador.PrepararAsync());

        Assert.Equal(MotivoFalloArranque.MigracionFallida, ex.Motivo);
        Assert.Contains(_motorBackup.NombreArchivoGenerado, ex.Message);
        Assert.Same(original, ex.InnerException);
        Assert.Equal(["Evaluar", "Backup", "Inicializar"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_NuevaInstalacionYMigracionFalla_PropagaExcepcionOriginal()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos([], ["mig1"], []);
        var original = new InvalidOperationException("Falla en nueva instalación");
        _inicializador.ExcepcionInicializar = original;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _actualizador.PrepararAsync());

        Assert.Same(original, ex);
        Assert.Equal(["Evaluar", "Inicializar"], _llamadas);
    }

    [Fact]
    public async Task PrepararAsync_CopiaLanzaOperationCanceled_PropagaSinEnvolver()
    {
        _inicializador.Estado = new EstadoEsquemaBaseDatos(["mig1"], ["mig2"], []);
        _motorBackup.ExcepcionCrear = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _actualizador.PrepararAsync());
    }

    [Fact]
    public async Task RegistrarAuditoriaAsync_Actualizada_RegistraFilaAuditoria_AlDia_NoRegistra()
    {
        await using var bd = await BaseDatosEnMemoria.CrearAsync();
        var actualizador = new ActualizadorBaseDatos(
            _inicializador,
            _motorBackup,
            bd.UnidadDeTrabajoFactory,
            _tiempo,
            NullLogger<ActualizadorBaseDatos>.Instance);

        var resultadoActualizada = new ResultadoActualizacionBaseDatos(
            EstadoActualizacionBaseDatos.Actualizada,
            ["mig2", "mig3"],
            "PesajeFaeneo_premigracion_2026-10-03_100000.zip");

        await actualizador.RegistrarAuditoriaAsync(resultadoActualizada);

        await using (var ctx = await bd.ContextFactory.CreateDbContextAsync())
        {
            var logs = await ctx.Auditorias.ToListAsync();
            var log = Assert.Single(logs);
            Assert.Equal(AccionAuditoria.ActualizacionSistema, log.Accion);
            Assert.Equal(EntidadesAuditoria.Sistema, log.Entidad);
            Assert.Null(log.UsuarioId);
            Assert.NotNull(log.DatosDespuesJson);
            Assert.Contains("PesajeFaeneo_premigracion_2026-10-03_100000.zip", log.DatosDespuesJson);
            Assert.Contains("mig2", log.DatosDespuesJson);
        }

        var resultadoAlDia = new ResultadoActualizacionBaseDatos(
            EstadoActualizacionBaseDatos.AlDia,
            [],
            null);

        await actualizador.RegistrarAuditoriaAsync(resultadoAlDia);

        await using (var ctx = await bd.ContextFactory.CreateDbContextAsync())
        {
            var logs = await ctx.Auditorias.ToListAsync();
            Assert.Single(logs); // Sigue siendo solo 1, no se agregó ninguna fila nueva
        }
    }
}
