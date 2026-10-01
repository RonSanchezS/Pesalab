namespace PesajeFaeneo.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using Xunit;

public class AlmacenConfiguracionBackupJsonTests
{
    [Fact]
    public async Task LeerAsync_SinArchivo_DevuelvePorDefecto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var config = await almacen.LeerAsync();

            Assert.Equal(ConfiguracionBackup.PorDefecto, config);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GuardarYLeer_IdaYVueltaCorrecta()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var guardada = new ConfiguracionBackup(false, false, 45, @"D:\CopiaSecundaria");

            await almacen.GuardarAsync(guardada);
            var leida = await almacen.LeerAsync();

            Assert.Equal(guardada, leida);
            Assert.False(File.Exists(Path.Combine(appPaths.ConfigDir, "backup.json.tmp")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LeerAsync_JsonRoto_DevuelvePorDefecto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            File.WriteAllText(Path.Combine(appPaths.ConfigDir, "backup.json"), "{ json roto");
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var config = await almacen.LeerAsync();

            Assert.Equal(ConfiguracionBackup.PorDefecto, config);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LeerAsync_VersionIncompatible_DevuelvePorDefecto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            File.WriteAllText(Path.Combine(appPaths.ConfigDir, "backup.json"), "{\"Version\":2,\"AutomaticoActivo\":true,\"AlCerrar\":true,\"DiasRetencion\":30,\"CarpetaSecundaria\":null}");
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var config = await almacen.LeerAsync();

            Assert.Equal(ConfiguracionBackup.PorDefecto, config);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LeerAsync_DiasRetencionInvalidoEnArchivo_DevuelvePorDefecto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            File.WriteAllText(Path.Combine(appPaths.ConfigDir, "backup.json"), "{\"Version\":1,\"AutomaticoActivo\":true,\"AlCerrar\":true,\"DiasRetencion\":400,\"CarpetaSecundaria\":null}");
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var config = await almacen.LeerAsync();

            Assert.Equal(ConfiguracionBackup.PorDefecto, config);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GuardarAsync_ConfiguracionInvalida_LanzaValidacionExceptionYNoCreaArchivo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cfg_bak_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(tempDir);
        appPaths.AsegurarDirectorios();
        try
        {
            var almacen = new AlmacenConfiguracionBackupJson(appPaths, NullLogger<AlmacenConfiguracionBackupJson>.Instance);
            var invalida = new ConfiguracionBackup(true, true, 5, null);

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(invalida));
            Assert.Contains(ex.Errores, e => e.Campo == "DiasRetencion");
            Assert.False(File.Exists(Path.Combine(appPaths.ConfigDir, "backup.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
