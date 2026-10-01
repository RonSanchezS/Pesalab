namespace PesajeFaeneo.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using PesajeFaeneo.Tests.Utilidades.Backup;
using Xunit;

public class AplicadorRestauracionTests
{
    [Fact]
    public async Task Test01_AplicarPendiente_SinMarcador_DevuelveNoHabiaYBorraHuerfanas()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        // Crear carpetas huérfanas validacion-x y pendiente-y (32 hex)
        var valHuerfana = Path.Combine(entorno.AppPaths.RestoreDir, $"validacion-{Guid.NewGuid():N}");
        var penHuerfana = Path.Combine(entorno.AppPaths.RestoreDir, $"pendiente-{Guid.NewGuid():N}");
        Directory.CreateDirectory(valHuerfana);
        Directory.CreateDirectory(penHuerfana);

        var resultado = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.NoHabia, resultado.Estado);
        Assert.False(Directory.Exists(valHuerfana));
        Assert.False(Directory.Exists(penHuerfana));
    }

    [Fact]
    public async Task Test02_Test09_FlujoCompleto_AplicaRestauracionYPermiteRevertir()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor("EQUIPO-TEST");
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        // Sembrar 5 boletas + logo-a.png
        await entorno.SembrarAsync(5);
        var rutaLogoA = Path.Combine(entorno.AppPaths.LogoDir, "logo-a.png");
        File.WriteAllBytes(rutaLogoA, new byte[] { 1, 2, 3 });

        // Crear backup manual
        var backup = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        // Sembrar 3 boletas más (total 8) y reemplazar logo por logo-b.png
        entorno.Tiempo.Advance(TimeSpan.FromHours(1));
        await entorno.SembrarAsync(3, "G2");
        Assert.Equal(8, await entorno.ContarPesajesAsync());

        File.Delete(rutaLogoA);
        var rutaLogoB = Path.Combine(entorno.AppPaths.LogoDir, "logo-b.png");
        File.WriteAllBytes(rutaLogoB, new byte[] { 4, 5, 6 });

        // Validar y programar restauración
        var val = await motor.ValidarParaRestaurarAsync(backup.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val.Token, "admin");

        // Test 02: Aplicar restauración
        var aplicada = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Aplicada, aplicada.Estado);
        Assert.Equal(backup.Backup.NombreArchivo, aplicada.ArchivoOrigen);
        Assert.Equal("admin", aplicada.SolicitadoPor);
        Assert.NotNull(aplicada.CarpetaReemplazada);
        Assert.True(Directory.Exists(aplicada.CarpetaReemplazada));

        // Base restaurada a 5 boletas
        Assert.Equal(5, await entorno.ContarPesajesAsync());

        // Logos: logo-a existe, logo-b no
        Assert.True(File.Exists(Path.Combine(entorno.AppPaths.LogoDir, "logo-a.png")));
        Assert.False(File.Exists(Path.Combine(entorno.AppPaths.LogoDir, "logo-b.png")));

        // Marcador y pendiente eliminados
        Assert.False(File.Exists(Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json")));
        Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "pendiente-*"));

        // Carpeta reemplazada tiene pesaje.db con 8 boletas y logo/logo-b.png
        var dbReemplazada = Path.Combine(aplicada.CarpetaReemplazada, "pesaje.db");
        Assert.True(File.Exists(dbReemplazada));
        Assert.True(File.Exists(Path.Combine(aplicada.CarpetaReemplazada, "logo", "logo-b.png")));

        var sb = new SqliteConnectionStringBuilder { DataSource = dbReemplazada, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using (var conn = new SqliteConnection(sb.ToString()))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM Pesajes;";
            Assert.Equal(8L, Convert.ToInt64(cmd.ExecuteScalar()));
        }

        // DatabaseInitializer real sobre la base restaurada funciona y journal_mode es WAL
        var initializer = new DatabaseInitializer(entorno.ContextFactory);
        await initializer.InicializarAsync();

        var sbActual = new SqliteConnectionStringBuilder { DataSource = entorno.AppPaths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using (var conn = new SqliteConnection(sbActual.ToString()))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("wal", cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant());
        }

        // Test 09: Revertir
        aplicador.Revertir(aplicada);

        // Base vuelve a tener 8 boletas
        Assert.Equal(8, await entorno.ContarPesajesAsync());
        Assert.True(File.Exists(Path.Combine(entorno.AppPaths.LogoDir, "logo-b.png")));
        Assert.False(File.Exists(Path.Combine(entorno.AppPaths.LogoDir, "logo-a.png")));

        var fallidas = Directory.GetDirectories(entorno.AppPaths.RestoreDir, "fallida-*");
        Assert.Single(fallidas);
    }

    [Fact]
    public async Task Test03_AplicarPendiente_ShaAlterado_DevuelveFallidaYConservaBase()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(4);
        var motor = entorno.CrearMotor("EQUIPO-TEST");
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var backup = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var val = await motor.ValidarParaRestaurarAsync(backup.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val.Token, "admin");

        // Alterar un byte en pendiente-*/pesaje.db
        var penDir = Directory.GetDirectories(entorno.AppPaths.RestoreDir, "pendiente-*").Single();
        var dbPendiente = Path.Combine(penDir, "pesaje.db");
        var bytes = File.ReadAllBytes(dbPendiente);
        bytes[bytes.Length - 1] = (byte)(bytes[bytes.Length - 1] ^ 0xFF);
        File.WriteAllBytes(dbPendiente, bytes);

        var resultado = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Fallida, resultado.Estado);
        Assert.Equal(4, await entorno.ContarPesajesAsync());
        Assert.False(File.Exists(Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json")));
    }

    [Fact]
    public async Task Test04_AplicarPendiente_CarpetaInexistente_DevuelveFallidaYBorraMarcador()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var rutaMarcador = Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json");
        var marcador = new MarcadorRestauracion(
            1,
            $"pendiente-{Guid.NewGuid():N}",
            "sha256fake",
            "backup.zip",
            DateTime.UtcNow,
            "admin",
            DateTime.UtcNow);
        File.WriteAllText(rutaMarcador, System.Text.Json.JsonSerializer.Serialize(marcador, JsonBackup.Opciones));

        var resultado = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Fallida, resultado.Estado);
        Assert.False(File.Exists(rutaMarcador));
    }

    [Fact]
    public async Task Test05_AplicarPendiente_MarcadorNoJson_DevuelveFallida()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var rutaMarcador = Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json");
        File.WriteAllText(rutaMarcador, "esto no es json");

        var resultado = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Fallida, resultado.Estado);
        Assert.False(File.Exists(rutaMarcador));
    }

    [Fact]
    public async Task Test06_AplicarPendiente_CarpetaConPathTraversal_DevuelveFallida()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(2);
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var rutaMarcador = Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json");
        var marcador = new MarcadorRestauracion(
            1,
            @"..\..\x",
            "sha256fake",
            "backup.zip",
            DateTime.UtcNow,
            "admin",
            DateTime.UtcNow);
        File.WriteAllText(rutaMarcador, System.Text.Json.JsonSerializer.Serialize(marcador, JsonBackup.Opciones));

        var resultado = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Fallida, resultado.Estado);
        Assert.Equal(2, await entorno.ContarPesajesAsync());
    }

    [Fact]
    public async Task Test07_AplicarPendiente_BaseActualBloqueada_DeshaceYDevuelveFallida()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(3);
        var motor = entorno.CrearMotor("EQUIPO-TEST");
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var backup = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var val = await motor.ValidarParaRestaurarAsync(backup.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val.Token, "admin");

        FileStream? bloqueo = null;
        try
        {
            bloqueo = new FileStream(
                entorno.AppPaths.DatabasePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            var resultado = aplicador.AplicarPendiente();

            Assert.Equal(EstadoRestauracionArranque.Fallida, resultado.Estado);
            Assert.Contains("otro programa está usando la base de datos", resultado.Mensaje);
            Assert.False(File.Exists(Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json")));
        }
        finally
        {
            bloqueo?.Dispose();
        }

        Assert.Equal(3, await entorno.ContarPesajesAsync());
    }

    [Fact]
    public async Task Test08_AplicarPendiente_ConWalYShmPresentes_SeMuevenAReemplazada()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(2);
        var motor = entorno.CrearMotor("EQUIPO-TEST");
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var backup = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var val = await motor.ValidarParaRestaurarAsync(backup.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val.Token, "admin");

        SqliteConnection.ClearAllPools();
        var walPath = entorno.AppPaths.DatabasePath + "-wal";
        var shmPath = entorno.AppPaths.DatabasePath + "-shm";
        File.WriteAllText(walPath, "dummy wal");
        File.WriteAllText(shmPath, "dummy shm");

        var aplicada = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Aplicada, aplicada.Estado);
        Assert.False(File.Exists(walPath));
        Assert.False(File.Exists(shmPath));

        Assert.NotNull(aplicada.CarpetaReemplazada);
        Assert.True(File.Exists(Path.Combine(aplicada.CarpetaReemplazada, "pesaje.db-wal")));
        Assert.True(File.Exists(Path.Combine(aplicada.CarpetaReemplazada, "pesaje.db-shm")));
    }

    [Fact]
    public async Task Test10_AplicarPendiente_RetencionReemplazadas_ConservaTresMasRecientes()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(1);
        var motor = entorno.CrearMotor("EQUIPO-TEST");
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        // Crear 4 carpetas reemplazada previas
        for (int i = 1; i <= 4; i++)
        {
            var dir = Path.Combine(entorno.AppPaths.RestoreDir, $"reemplazada-20261001_0{i}0000");
            Directory.CreateDirectory(dir);
        }

        var backup = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var val = await motor.ValidarParaRestaurarAsync(backup.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val.Token, "admin");

        var aplicada = aplicador.AplicarPendiente();

        Assert.Equal(EstadoRestauracionArranque.Aplicada, aplicada.Estado);
        var carpetas = Directory.GetDirectories(entorno.AppPaths.RestoreDir, "reemplazada-*");
        Assert.Equal(3, carpetas.Length);
    }

    [Fact]
    public async Task Test11_RegistrarAuditoriaAsync_CreaFilaAuditoriaConDatosCorrectos()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var aplicador = new AplicadorRestauracion(
            entorno.AppPaths,
            entorno.ContextFactory,
            entorno.Tiempo,
            NullLogger<AplicadorRestauracion>.Instance);

        var aplicada = new ResultadoRestauracionArranque(
            EstadoRestauracionArranque.Aplicada,
            null,
            "PesajeFaeneo_manual_2026-10-01.zip",
            new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            "admin_test",
            "carpeta_reemplazada");

        await aplicador.RegistrarAuditoriaAsync(aplicada);

        await using var ctx = await entorno.ContextFactory.CreateDbContextAsync();
        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.Accion == AccionAuditoria.Restauracion);

        Assert.NotNull(auditoria);
        Assert.Null(auditoria.UsuarioId);
        Assert.Equal(EntidadesAuditoria.Sistema, auditoria.Entidad);
        Assert.Equal("Restauracion", auditoria.EntidadId);
        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("admin_test", auditoria.DatosDespuesJson);
        Assert.Contains("PesajeFaeneo_manual_2026-10-01.zip", auditoria.DatosDespuesJson);
    }
}
