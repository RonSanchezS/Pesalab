namespace PesajeFaeneo.Tests.Utilidades.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public sealed class MotorBackupFalso : IMotorBackup
{
    public List<SolicitudBackup> Solicitudes { get; } = new();
    public List<(string RutaBackup, string RutaDestino)> Exportaciones { get; } = new();
    public List<string> Validaciones { get; } = new();
    public List<(string Token, string SolicitadoPor)> Programaciones { get; } = new();
    public List<string> Descartes { get; } = new();
    public List<string> CarpetasVerificadas { get; } = new();

    public bool ExisteDelDia { get; set; }
    public IReadOnlyList<InfoBackup> Lista { get; set; } = Array.Empty<InfoBackup>();
    public ValidacionRestauracion? Validacion { get; set; }
    public InfoBackup? Resguardo { get; set; }
    public string CarpetaBackups { get; set; } = @"C:\FakeBackups";

    public Exception? ErrorCrear { get; set; }
    public Exception? ErrorValidar { get; set; }
    public Exception? ErrorProgramar { get; set; }
    public Exception? ErrorVerificarCarpeta { get; set; }

    public TaskCompletionSource PrimeraCreacion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ResultadoCreacionBackup> CrearAsync(SolicitudBackup solicitud, CancellationToken cancellationToken = default)
    {
        Solicitudes.Add(solicitud);
        PrimeraCreacion.TrySetResult();

        if (ErrorCrear is not null)
        {
            throw ErrorCrear;
        }

        var backup = new InfoBackup(
            $@"C:\FakeBackups\{solicitud.Tipo.ToString().ToLowerInvariant()}\fake.zip",
            "fake.zip",
            solicitud.Tipo,
            DateTime.UtcNow,
            1024,
            "1.0.0",
            "EQUIPO-TEST",
            "migracion_fake",
            10,
            10L,
            Valido: true,
            Problema: null);

        return Task.FromResult(new ResultadoCreacionBackup(backup, null));
    }

    public Task<bool> ExisteAutomaticoDelDiaAsync(DateOnly fecha, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExisteDelDia);
    }

    public Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Lista);
    }

    public Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default)
    {
        Exportaciones.Add((rutaBackup, rutaDestino));
        return Task.CompletedTask;
    }

    public Task<ValidacionRestauracion> ValidarParaRestaurarAsync(string rutaBackup, CancellationToken cancellationToken = default)
    {
        Validaciones.Add(rutaBackup);
        if (ErrorValidar is not null)
        {
            throw ErrorValidar;
        }

        if (Validacion is null)
        {
            var info = new InfoBackup(
                rutaBackup,
                Path.GetFileName(rutaBackup),
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                5,
                5L,
                Valido: true,
                Problema: null);

            return Task.FromResult(new ValidacionRestauracion(
                $"validacion-{Guid.NewGuid():N}",
                info,
                1,
                1,
                1,
                1,
                false,
                Array.Empty<string>()));
        }

        return Task.FromResult(Validacion);
    }

    public Task<InfoBackup> ProgramarRestauracionAsync(string token, string solicitadoPor, CancellationToken cancellationToken = default)
    {
        Programaciones.Add((token, solicitadoPor));
        if (ErrorProgramar is not null)
        {
            throw ErrorProgramar;
        }

        var resguardo = Resguardo ?? new InfoBackup(
            $@"C:\FakeBackups\seguridad\PesajeFaeneo_prerestauracion_fake.zip",
            "PesajeFaeneo_prerestauracion_fake.zip",
            TipoBackup.PreRestauracion,
            DateTime.UtcNow,
            1024,
            "1.0.0",
            "EQUIPO-TEST",
            "migracion_fake",
            5,
            5L,
            Valido: true,
            Problema: null);

        return Task.FromResult(resguardo);
    }

    public Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default)
    {
        Descartes.Add(token);
        return Task.CompletedTask;
    }

    public Task VerificarCarpetaEscribibleAsync(string carpeta, CancellationToken cancellationToken = default)
    {
        CarpetasVerificadas.Add(carpeta);
        if (ErrorVerificarCarpeta is not null)
        {
            throw ErrorVerificarCarpeta;
        }

        return Task.CompletedTask;
    }
}

public sealed class AlmacenConfiguracionBackupFalso : IAlmacenConfiguracionBackup
{
    public ConfiguracionBackup Configuracion { get; set; } = ConfiguracionBackup.PorDefecto;
    public int Guardados { get; private set; }

    public Task<ConfiguracionBackup> LeerAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Configuracion);
    }

    public Task GuardarAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default)
    {
        Configuracion = configuracion;
        Guardados++;
        return Task.CompletedTask;
    }
}

public sealed class ProgramadorBackupFalso : IProgramadorBackupAutomatico
{
    public ResultadoBackupAutomatico? UltimoResultado { get; set; }
    public bool Iniciado { get; private set; }

    public void Iniciar()
    {
        Iniciado = true;
    }

    public Task DetenerAsync()
    {
        Iniciado = false;
        return Task.CompletedTask;
    }

    public Task<ResultadoBackupAutomatico?> EjecutarAlCerrarAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UltimoResultado);
    }
}

public sealed class ServicioBackupFalso : IServicioBackup
{
    public string CarpetaBackups { get; set; } = @"C:\FakeBackups";
    public ResultadoBackupAutomatico? UltimoAutomatico { get; set; }
    public List<InfoBackup> Lista { get; set; } = new();
    public int ConteoCrearManual { get; private set; }
    public int ConteoListar { get; private set; }
    public List<(string RutaBackup, string RutaDestino)> Exportaciones { get; } = new();
    public List<string> Validaciones { get; } = new();
    public List<(string Token, string Confirmacion)> Programaciones { get; } = new();
    public List<string> Descartes { get; } = new();
    public List<ConfiguracionBackup> ConfiguracionesGuardadas { get; } = new();

    public ResultadoCreacionBackup? ResultadoCrearManual { get; set; }
    public ValidacionRestauracion? ResultadoValidacion { get; set; }
    public InfoBackup? ResultadoProgramacion { get; set; }
    public ConfiguracionBackup Configuracion { get; set; } = ConfiguracionBackup.PorDefecto;

    public Exception? ErrorCrearManual { get; set; }
    public Exception? ErrorExportar { get; set; }
    public Exception? ErrorValidar { get; set; }
    public Exception? ErrorProgramar { get; set; }
    public Exception? ErrorGuardarConfiguracion { get; set; }

    public Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default)
    {
        ConteoListar++;
        return Task.FromResult<IReadOnlyList<InfoBackup>>(Lista);
    }

    public Task<ResultadoCreacionBackup> CrearManualAsync(CancellationToken cancellationToken = default)
    {
        ConteoCrearManual++;
        if (ErrorCrearManual != null) throw ErrorCrearManual;

        var res = ResultadoCrearManual ?? new ResultadoCreacionBackup(
            new InfoBackup(
                $@"{CarpetaBackups}\manuales\PesajeFaeneo_manual_test.zip",
                "PesajeFaeneo_manual_test.zip",
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                10,
                10L,
                Valido: true,
                Problema: null),
            null);
        return Task.FromResult(res);
    }

    public Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default)
    {
        Exportaciones.Add((rutaBackup, rutaDestino));
        if (ErrorExportar != null) throw ErrorExportar;
        return Task.CompletedTask;
    }

    public Task<ValidacionRestauracion> ValidarAsync(string rutaBackup, CancellationToken cancellationToken = default)
    {
        Validaciones.Add(rutaBackup);
        if (ErrorValidar != null) throw ErrorValidar;

        var val = ResultadoValidacion ?? new ValidacionRestauracion(
            "token-test",
            new InfoBackup(
                rutaBackup,
                Path.GetFileName(rutaBackup),
                TipoBackup.Manual,
                DateTime.UtcNow,
                1024,
                "1.0.0",
                "EQUIPO-TEST",
                "migracion_fake",
                5,
                5L,
                Valido: true,
                Problema: null),
            1, 1, 1, 1, false, Array.Empty<string>());
        return Task.FromResult(val);
    }

    public Task<InfoBackup> ProgramarRestauracionAsync(string token, string confirmacion, CancellationToken cancellationToken = default)
    {
        Programaciones.Add((token, confirmacion));
        if (ErrorProgramar != null) throw ErrorProgramar;

        var resguardo = ResultadoProgramacion ?? new InfoBackup(
            $@"{CarpetaBackups}\seguridad\PesajeFaeneo_prerestauracion_test.zip",
            "PesajeFaeneo_prerestauracion_test.zip",
            TipoBackup.PreRestauracion,
            DateTime.UtcNow,
            1024,
            "1.0.0",
            "EQUIPO-TEST",
            "migracion_fake",
            5,
            5L,
            Valido: true,
            Problema: null);
        return Task.FromResult(resguardo);
    }

    public Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default)
    {
        Descartes.Add(token);
        return Task.CompletedTask;
    }

    public Task<ConfiguracionBackup> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Configuracion);
    }

    public Task GuardarConfiguracionAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default)
    {
        if (ErrorGuardarConfiguracion != null) throw ErrorGuardarConfiguracion;
        Configuracion = configuracion;
        ConfiguracionesGuardadas.Add(configuracion);
        return Task.CompletedTask;
    }
}

