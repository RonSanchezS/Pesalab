namespace PesajeFaeneo.Tests;

using System.IO.Compression;
using Microsoft.Data.Sqlite;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using PesajeFaeneo.Tests.Utilidades.Backup;
using Xunit;

public class SqliteBackupServiceTests
{
    [Fact]
    public async Task Test01_CrearAsync_Manual_CreaArchivoCorrecto()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(3);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var resultado = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        Assert.NotNull(resultado.Backup);
        Assert.Null(resultado.AdvertenciaCopiaSecundaria);
        Assert.True(File.Exists(resultado.Backup.RutaArchivo));
        Assert.Equal("PesajeFaeneo_manual_2026-10-01_100000.zip", resultado.Backup.NombreArchivo);

        using var zip = ZipFile.OpenRead(resultado.Backup.RutaArchivo);
        var entradas = zip.Entries.Select(e => e.FullName).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "manifest.json", "pesaje.db" }, entradas);
    }

    [Fact]
    public async Task Test02_BaseExtraida_TieneJournalModeDeleteYQuickCheckOk()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(5);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var resultado = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var tempDir = Path.Combine(Path.GetTempPath(), $"ext_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var manifiesto = ArchivoBackupZip.ExtraerValidado(resultado.Backup.RutaArchivo, tempDir);
            var rutaDbExtraida = Path.Combine(tempDir, "pesaje.db");

            var connStr = new SqliteConnectionStringBuilder { DataSource = rutaDbExtraida, Pooling = false }.ToString();
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode;";
                Assert.Equal("delete", cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA quick_check;";
                Assert.Equal("ok", cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant());
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM Pesajes;";
                Assert.Equal(5L, Convert.ToInt64(cmd.ExecuteScalar()));
            }

            Assert.Equal("EQUIPO-TEST", manifiesto.Equipo);
            Assert.Equal(TipoBackup.Manual, manifiesto.Tipo);
            Assert.Equal(5, manifiesto.Pesajes);
            Assert.Equal(5L, manifiesto.UltimoNumeroBoleta);
            Assert.Equal("20261008185354_F11_ModoDesposte", manifiesto.UltimaMigracion);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test03_ConsistenciaConWal_CopiaDatosConfirmadosYNoLosNoConfirmados()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(0);

        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = entorno.AppPaths.DatabasePath,
            Pooling = false
        }.ToString();

        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_autocheckpoint=0;";
            cmd.ExecuteNonQuery();
        }

        // Insertar y confirmar ZZ
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Ganaderos (Codigo, Nombre, Activo, CreadoEn) VALUES ('ZZ', 'GANADERO ZZ', 1, '2026-10-01 10:00:00');";
            cmd.ExecuteNonQuery();
        }

        // Iniciar transacción e insertar YY sin confirmar
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Ganaderos (Codigo, Nombre, Activo, CreadoEn) VALUES ('YY', 'GANADERO YY', 1, '2026-10-01 10:00:00');";
            cmd.ExecuteNonQuery();
        }

        var motor = entorno.CrearMotor();
        var resultado = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        // Revertir y cerrar
        tx.Rollback();
        conn.Close();

        var tempDir = Path.Combine(Path.GetTempPath(), $"ext_wal_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            ArchivoBackupZip.ExtraerValidado(resultado.Backup.RutaArchivo, tempDir);
            var rutaDb = Path.Combine(tempDir, "pesaje.db");
            var connStrExtraida = new SqliteConnectionStringBuilder { DataSource = rutaDb, Pooling = false }.ToString();
            using var connExtraida = new SqliteConnection(connStrExtraida);
            connExtraida.Open();
            using var cmd = connExtraida.CreateCommand();

            cmd.CommandText = "SELECT count(*) FROM Ganaderos WHERE Codigo = 'ZZ';";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

            cmd.CommandText = "SELECT count(*) FROM Ganaderos WHERE Codigo = 'YY';";
            Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test04_TrasCrearAsync_NoQuedanCarpetasTemporalesYSePuedeBorrarZip()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        var resultado = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        var directoriosTmp = Directory.GetDirectories(entorno.AppPaths.BackupsDir, ".tmp-*");
        Assert.Empty(directoriosTmp);

        // File.Delete debe funcionar de inmediato sin IOException
        File.Delete(resultado.Backup.RutaArchivo);
        Assert.False(File.Exists(resultado.Backup.RutaArchivo));
    }

    [Fact]
    public async Task Test05_Logos_SoloIncluyeLogosValidosHastaMaximo()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        Directory.CreateDirectory(entorno.AppPaths.LogoDir);

        File.WriteAllBytes(Path.Combine(entorno.AppPaths.LogoDir, "logo-1.png"), new byte[] { 137, 80, 78, 71 });
        File.WriteAllText(Path.Combine(entorno.AppPaths.LogoDir, "notas.txt"), "notas");
        // Logo de 1 048 577 bytes (supera el límite de 1MB)
        File.WriteAllBytes(Path.Combine(entorno.AppPaths.LogoDir, "grande.png"), new byte[1_048_577]);

        var motor = entorno.CrearMotor();
        var resultado = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        using var zip = ZipFile.OpenRead(resultado.Backup.RutaArchivo);
        var entries = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("logo/logo-1.png", entries);
        Assert.DoesNotContain("logo/notas.txt", entries);
        Assert.DoesNotContain("logo/grande.png", entries);
    }

    [Fact]
    public async Task Test06_Automatico_ReemplazaElDelMismoDia()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        var r1 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Automatico, 30, null));
        Assert.Equal("PesajeFaeneo_automatico_2026-10-01.zip", r1.Backup.NombreArchivo);

        Assert.True(await motor.ExisteAutomaticoDelDiaAsync(new DateOnly(2026, 10, 1)));
        Assert.False(await motor.ExisteAutomaticoDelDiaAsync(new DateOnly(2026, 10, 2)));

        // Avanzar el reloj 1 hora
        entorno.Tiempo.Advance(TimeSpan.FromHours(1));

        var r2 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Automatico, 30, null));
        Assert.Equal("PesajeFaeneo_automatico_2026-10-01.zip", r2.Backup.NombreArchivo);

        // Solo debe haber un archivo automático
        var archivos = Directory.GetFiles(Path.Combine(entorno.AppPaths.BackupsDir, "automaticos"), "*.zip");
        Assert.Single(archivos);

        var m = ArchivoBackupZip.LeerManifiesto(r2.Backup.RutaArchivo);
        Assert.NotNull(m);
        Assert.Equal(entorno.Tiempo.GetUtcNow().UtcDateTime, m.CreadoEnUtc);
    }

    [Fact]
    public async Task Test07_DosCrearAsyncManual_SinAvanzarReloj_GeneraNumeracion()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        var r1 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var r2 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        Assert.Equal("PesajeFaeneo_manual_2026-10-01_100000.zip", r1.Backup.NombreArchivo);
        Assert.Equal("PesajeFaeneo_manual_2026-10-01_100000_2.zip", r2.Backup.NombreArchivo);
    }

    [Fact]
    public async Task Test08_Retencion_EliminaSoloAutomaticosAntiguos()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var carpAuto = Path.Combine(entorno.AppPaths.BackupsDir, "automaticos");
        Directory.CreateDirectory(carpAuto);

        var hoy = new DateOnly(2026, 10, 1);
        for (int i = 1; i <= 40; i++)
        {
            var f = hoy.AddDays(-i);
            var rutaZip = Path.Combine(carpAuto, $"PesajeFaeneo_automatico_{f:yyyy-MM-dd}.zip");
            using var zip = ZipFile.Open(rutaZip, ZipArchiveMode.Create);
        }
        var rutaOtro = Path.Combine(carpAuto, "otro.zip");
        using (var zip = ZipFile.Open(rutaOtro, ZipArchiveMode.Create)) { }

        var motor = entorno.CrearMotor();
        await motor.CrearAsync(new SolicitudBackup(TipoBackup.Automatico, 30, null));

        var archivosRestantes = Directory.GetFiles(carpAuto, "*.zip").Select(Path.GetFileName).ToList();
        // 30 automáticos (días 0 a 29) + otro.zip = 31 archivos
        Assert.Equal(31, archivosRestantes.Count);
        Assert.Contains("otro.zip", archivosRestantes);
        Assert.Contains("PesajeFaeneo_automatico_2026-10-01.zip", archivosRestantes);
    }

    [Fact]
    public async Task Test09_CarpetaSecundaria_CopiaCorrectamenteOReportaAdvertencia()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        var carpSec = Path.Combine(Path.GetTempPath(), $"sec_{Guid.NewGuid():N}");
        try
        {
            // 1. Carpeta secundaria válida
            var rValido = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, carpSec));
            Assert.Null(rValido.AdvertenciaCopiaSecundaria);
            Assert.True(File.Exists(Path.Combine(carpSec, rValido.Backup.NombreArchivo)));

            // 2. Carpeta secundaria que falla al copiar (bloqueada por un archivo existente)
            var archivoBloqueador = Path.Combine(carpSec, "bloqueador.txt");
            File.WriteAllText(archivoBloqueador, "bloqueo");
            var rutaInvalida = Path.Combine(archivoBloqueador, "subcarpeta");
            var rInvalido = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, rutaInvalida));
            Assert.NotNull(rInvalido.AdvertenciaCopiaSecundaria);
            Assert.True(File.Exists(rInvalido.Backup.RutaArchivo));
        }
        finally
        {
            if (Directory.Exists(carpSec))
            {
                try { Directory.Delete(carpSec, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test10_EspacioLibreInsuficiente_LanzaExcepcion()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();
        motor.EspacioLibreParaTest = _ => 0;

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null)));

        Assert.Equal("BAK_ESPACIO", ex.Codigo);

        var carpManuales = Path.Combine(entorno.AppPaths.BackupsDir, "manuales");
        if (Directory.Exists(carpManuales))
        {
            var archivos = Directory.GetFiles(carpManuales, "*.zip");
            Assert.Empty(archivos);
        }
    }

    [Fact]
    public async Task Test11_ListarAsync_ListaOrdenadaYDetectaArchivosInvalidos()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        entorno.Tiempo.Advance(TimeSpan.FromMinutes(10));
        await motor.CrearAsync(new SolicitudBackup(TipoBackup.Automatico, 30, null));

        var carpSeg = Path.Combine(entorno.AppPaths.BackupsDir, "seguridad");
        Directory.CreateDirectory(carpSeg);
        File.WriteAllText(Path.Combine(carpSeg, "basura.zip"), "no es zip");

        var lista = await motor.ListarAsync();
        Assert.Equal(3, lista.Count);
        Assert.True(lista[0].CreadoEnUtc >= lista[1].CreadoEnUtc);

        var invalido = lista.Single(x => x.NombreArchivo == "basura.zip");
        Assert.False(invalido.Valido);
        Assert.Equal("No se pudo leer: archivo dañado o de otro sistema.", invalido.Problema);
    }

    [Fact]
    public async Task Test19_ExportarAsync_CopiaArchivoOInformaError()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();
        var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        var tempDir = Path.Combine(Path.GetTempPath(), $"exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var destinoValido = Path.Combine(tempDir, "exportado.zip");
            await motor.ExportarAsync(r.Backup.RutaArchivo, destinoValido);

            Assert.True(File.Exists(destinoValido));
            Assert.Equal(File.ReadAllBytes(r.Backup.RutaArchivo), File.ReadAllBytes(destinoValido));

            var destinoInexistente = Path.Combine(tempDir, "no-existe", "a.zip");
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                motor.ExportarAsync(r.Backup.RutaArchivo, destinoInexistente));
            Assert.Equal("BAK_EXPORTAR", ex.Codigo);

            var tmps = Directory.GetFiles(tempDir, "*.tmp");
            Assert.Empty(tmps);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test20_VerificarCarpetaEscribibleAsync_ValidaCarpetas()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor();

        // 1. Inexistente
        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() =>
            motor.VerificarCarpetaEscribibleAsync(@"C:\Carpeta\Inexistente\RandomGuid123"));
        Assert.Equal(CamposBackup.CarpetaSecundaria, ex1.Errores[0].Campo);

        // 2. Dentro de BaseDir
        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() =>
            motor.VerificarCarpetaEscribibleAsync(entorno.AppPaths.BackupsDir));
        Assert.Equal(CamposBackup.CarpetaSecundaria, ex2.Errores[0].Campo);

        // 3. Temporal válida
        var tempDir = Path.Combine(Path.GetTempPath(), $"esc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            await motor.VerificarCarpetaEscribibleAsync(tempDir);
            var archivos = Directory.GetFiles(tempDir);
            Assert.Empty(archivos);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test12_ValidarParaRestaurarAsync_BackupPropio_ValidaCorrectamente()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(4);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var validacion = await motor.ValidarParaRestaurarAsync(r.Backup.RutaArchivo);

        Assert.Matches(@"^validacion-[0-9a-f]{32}$", validacion.Token);
        Assert.Equal(4, validacion.Backup.Pesajes);
        Assert.False(validacion.RequiereActualizarFormato);
        Assert.Empty(validacion.Advertencias);

        // Con motor de equipo "OTRO" que creó el backup -> advertencia que contiene «OTRO»
        var motorOtro = entorno.CrearMotor("OTRO");
        entorno.Tiempo.Advance(TimeSpan.FromMinutes(5));
        var rOtro = await motorOtro.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var valOtro = await motor.ValidarParaRestaurarAsync(rOtro.Backup.RutaArchivo);
        Assert.Contains(valOtro.Advertencias, a => a.Contains("«OTRO»"));

        await motor.DescartarValidacionAsync(validacion.Token);
        await motor.DescartarValidacionAsync(valOtro.Token);
    }

    [Fact]
    public async Task Test13_ValidarParaRestaurarAsync_RechazaCasosInvalidosYSinCarpetasHuerfanas()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(1);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var tempDir = Path.Combine(Path.GetTempPath(), $"val_inval_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
            var baseExtraidaDir = Path.Combine(tempDir, "base_extraida");
            ArchivoBackupZip.ExtraerValidado(r.Backup.RutaArchivo, baseExtraidaDir);
            var dbLimpia = Path.Combine(baseExtraidaDir, "pesaje.db");

            // 1. Archivo de texto con extensión .zip
            var txtZip = Path.Combine(tempDir, "texto.zip");
            File.WriteAllText(txtZip, "no es un zip");
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(txtZip));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 2. Zip con ../x
            var zipPathTraversal = Path.Combine(tempDir, "traversal.zip");
            using (var zip = ZipFile.Open(zipPathTraversal, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("../x.txt");
                using var sw = new StreamWriter(entry.Open());
                sw.Write("peligro");
            }
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipPathTraversal));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 3. Zip con otro.txt
            var zipOtro = Path.Combine(tempDir, "otro.zip");
            using (var zip = ZipFile.Open(zipOtro, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("otro.txt");
                using var sw = new StreamWriter(entry.Open());
                sw.Write("desconocido");
            }
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipOtro));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 4. Zip sin manifiesto (solo pesaje.db)
            var zipSinManifiesto = Path.Combine(tempDir, "sinmanifiesto.zip");
            using (var zip = ZipFile.Open(zipSinManifiesto, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(dbLimpia, "pesaje.db");
            }
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipSinManifiesto));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 5. SHA alterado
            var zipShaAlterado = ZipBackupDePrueba.Crear(
                tempDir,
                dbLimpia,
                m => m with { Sha256BaseDatos = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" });
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipShaAlterado));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 6. pesaje.db truncada a la mitad
            var dbTruncada = Path.Combine(tempDir, "truncada.db");
            var bytesLimpia = File.ReadAllBytes(dbLimpia);
            File.WriteAllBytes(dbTruncada, bytesLimpia.Take(bytesLimpia.Length / 2).ToArray());
            var zipTruncado = ZipBackupDePrueba.Crear(tempDir, dbTruncada);
            await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipTruncado));
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));

            // 7. pesaje.db vacía (0 bytes) -> "no pertenece a este sistema"
            var dbVacia = Path.Combine(tempDir, "vacia.db");
            File.WriteAllBytes(dbVacia, Array.Empty<byte>());
            var zipVacio = ZipBackupDePrueba.Crear(tempDir, dbVacia);
            var exVacio = await Assert.ThrowsAsync<ValidacionException>(() => motor.ValidarParaRestaurarAsync(zipVacio));
            Assert.Contains("no pertenece a este sistema", exVacio.Message);
            Assert.Empty(Directory.GetDirectories(entorno.AppPaths.RestoreDir, "validacion-*"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test14_ValidarParaRestaurarAsync_MigracionDesconocida_LanzaBakVersionNueva()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(1);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var tempDir = Path.Combine(Path.GetTempPath(), $"val_futura_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
            var baseExtraidaDir = Path.Combine(tempDir, "base_extraida");
            ArchivoBackupZip.ExtraerValidado(r.Backup.RutaArchivo, baseExtraidaDir);
            var copiaDb = Path.Combine(baseExtraidaDir, "pesaje.db");

            var sb = new SqliteConnectionStringBuilder { DataSource = copiaDb, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
            using (var conn = new SqliteConnection(sb.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO __EFMigrationsHistory VALUES ('29990101000000_Futura', '8.0.11');";
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var zip = ZipBackupDePrueba.Crear(tempDir, copiaDb);
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => motor.ValidarParaRestaurarAsync(zip));
            Assert.Equal("BAK_VERSION_NUEVA", ex.Codigo);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test15_ValidarParaRestaurarAsync_SinAdminActivo_LanzaBakSinAdmin()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(1);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var tempDir = Path.Combine(Path.GetTempPath(), $"val_sinadmin_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
            var baseExtraidaDir = Path.Combine(tempDir, "base_extraida");
            ArchivoBackupZip.ExtraerValidado(r.Backup.RutaArchivo, baseExtraidaDir);
            var copiaDb = Path.Combine(baseExtraidaDir, "pesaje.db");

            var sb = new SqliteConnectionStringBuilder { DataSource = copiaDb, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
            using (var conn = new SqliteConnection(sb.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Usuarios SET Activo = 0;";
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var zip = ZipBackupDePrueba.Crear(tempDir, copiaDb);
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => motor.ValidarParaRestaurarAsync(zip));
            Assert.Equal("BAK_SIN_ADMIN", ex.Codigo);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test16_ValidarParaRestaurarAsync_VersionAnterior_PruebaMigracionYNoModificaCopia()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var tempDir = Path.Combine(Path.GetTempPath(), $"val_vieja_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tempDb = Path.Combine(tempDir, "vieja.db");
            var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PesajeFaeneo.Infrastructure.Persistencia.PesajeDbContext>();
            PesajeFaeneo.Infrastructure.Persistencia.SqliteOpcionesExtensiones.ConfigurarSqlite(
                builder,
                new SqliteConnectionStringBuilder { DataSource = tempDb, Pooling = false }.ToString());
            await using (var ctx = new PesajeFaeneo.Infrastructure.Persistencia.PesajeDbContext(builder.Options))
            {
                var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(ctx);
                await migrator.MigrateAsync("20260927144821_F4_Pesaje");

                // Insertar admin activo para pasar la validación de admin
                await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(
                    ctx.Database,
                    "INSERT INTO Usuarios (NombreUsuario, Hash, Salt, NombreCompleto, Rol, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn) " +
                    "VALUES ('admin','h','s','Admin',1,1,0,0,'2026-01-01 00:00:00')");
            }

            var zip = ZipBackupDePrueba.Crear(tempDir, tempDb);
            var validacion = await motor.ValidarParaRestaurarAsync(zip);

            Assert.True(validacion.RequiereActualizarFormato);
            Assert.Contains(validacion.Advertencias, a => a.Contains("versión anterior"));

            // Verificar que pesaje.db dentro de la carpeta de validación NO tiene columna Version
            var carpetaVal = Path.Combine(entorno.AppPaths.RestoreDir, validacion.Token);
            var dbEnValidacion = Path.Combine(carpetaVal, "pesaje.db");
            var columnas = new List<string>();
            var sb = new SqliteConnectionStringBuilder { DataSource = dbEnValidacion, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            using (var conn = new SqliteConnection(sb.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(Pesajes);";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    columnas.Add(reader.GetString(1));
                }
            }
            Assert.DoesNotContain("Version", columnas);

            await motor.DescartarValidacionAsync(validacion.Token);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test17_ProgramarRestauracionAsync_CreaResguardoYMueveCarpeta()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(3);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var r = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        var validacion = await motor.ValidarParaRestaurarAsync(r.Backup.RutaArchivo);

        // Token inválido "../x" -> ValidacionException campo Archivo
        var ex = await Assert.ThrowsAsync<ValidacionException>(() =>
            motor.ProgramarRestauracionAsync(@"..\x", "admin"));
        Assert.Equal(CamposBackup.Archivo, ex.Errores[0].Campo);

        // Programar correctamente
        var resguardo = await motor.ProgramarRestauracionAsync(validacion.Token, "admin");
        Assert.NotNull(resguardo);
        Assert.Equal(TipoBackup.PreRestauracion, resguardo.Tipo);
        Assert.True(File.Exists(resguardo.RutaArchivo));

        // La carpeta de validación pasó a pendiente-*
        Assert.False(Directory.Exists(Path.Combine(entorno.AppPaths.RestoreDir, validacion.Token)));
        var pendientes = Directory.GetDirectories(entorno.AppPaths.RestoreDir, "pendiente-*");
        Assert.Single(pendientes);

        // pendiente.json
        var rutaMarcador = Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json");
        Assert.True(File.Exists(rutaMarcador));
        var json = File.ReadAllText(rutaMarcador);
        var marcador = System.Text.Json.JsonSerializer.Deserialize<MarcadorRestauracion>(json, JsonBackup.Opciones)!;
        Assert.Equal(1, marcador.Version);
        Assert.Equal(Path.GetFileName(pendientes[0]), marcador.Carpeta);
        var maniOriginal = ArchivoBackupZip.LeerManifiesto(r.Backup.RutaArchivo)!;
        Assert.Equal(maniOriginal.Sha256BaseDatos, marcador.Sha256BaseDatos);
        Assert.Equal("admin", marcador.SolicitadoPor);
        Assert.Equal(r.Backup.NombreArchivo, marcador.ArchivoOrigen);

        // Repetir el mismo token -> BAK_VALIDACION_VENCIDA
        var exRepetido = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            motor.ProgramarRestauracionAsync(validacion.Token, "admin"));
        Assert.Equal("BAK_VALIDACION_VENCIDA", exRepetido.Codigo);
    }

    [Fact]
    public async Task Test18_DosValidacionesYProgramacionesSeguidas_QuedaUnSoloPendienteYMarcadorCorrecto()
    {
        await using var entorno = await EntornoArchivosPrueba.CrearAsync();
        await entorno.SembrarAsync(2);
        var motor = entorno.CrearMotor("EQUIPO-TEST");

        var r1 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));
        entorno.Tiempo.Advance(TimeSpan.FromMinutes(10));
        await entorno.SembrarAsync(3);
        var r2 = await motor.CrearAsync(new SolicitudBackup(TipoBackup.Manual, 30, null));

        var val1 = await motor.ValidarParaRestaurarAsync(r1.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val1.Token, "admin1");

        var val2 = await motor.ValidarParaRestaurarAsync(r2.Backup.RutaArchivo);
        await motor.ProgramarRestauracionAsync(val2.Token, "admin2");

        var pendientes = Directory.GetDirectories(entorno.AppPaths.RestoreDir, "pendiente-*");
        Assert.Single(pendientes);

        var marcador = System.Text.Json.JsonSerializer.Deserialize<MarcadorRestauracion>(
            File.ReadAllText(Path.Combine(entorno.AppPaths.RestoreDir, "pendiente.json")),
            JsonBackup.Opciones)!;
        Assert.Equal(Path.GetFileName(pendientes[0]), marcador.Carpeta);
        Assert.Equal("admin2", marcador.SolicitadoPor);
        Assert.Equal(r2.Backup.NombreArchivo, marcador.ArchivoOrigen);
    }
}
