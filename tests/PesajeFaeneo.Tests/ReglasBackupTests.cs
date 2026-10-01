namespace PesajeFaeneo.Tests;

using System.Globalization;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using Xunit;

public class ReglasBackupTests
{
    [Fact]
    public void NombreArchivo_GeneraNombresEsperadosEInvarianteDeCultura()
    {
        var prevCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var fecha = new DateTime(2026, 10, 1, 10, 0, 5);

            Assert.Equal("PesajeFaeneo_automatico_2026-10-01.zip", ReglasBackup.NombreArchivo(TipoBackup.Automatico, fecha));
            Assert.Equal("PesajeFaeneo_manual_2026-10-01_100005.zip", ReglasBackup.NombreArchivo(TipoBackup.Manual, fecha));
            Assert.Equal("PesajeFaeneo_prerestauracion_2026-10-01_100005.zip", ReglasBackup.NombreArchivo(TipoBackup.PreRestauracion, fecha));
            Assert.Equal("PesajeFaeneo_preimportacion_2026-10-01_100005.zip", ReglasBackup.NombreArchivo(TipoBackup.PreImportacion, fecha));
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
        }
    }

    [Fact]
    public void TryLeerFechaAutomatico_ValidaFormatoYValores()
    {
        Assert.True(ReglasBackup.TryLeerFechaAutomatico("PesajeFaeneo_automatico_2026-10-01.zip", out var fecha));
        Assert.Equal(new DateOnly(2026, 10, 1), fecha);

        Assert.False(ReglasBackup.TryLeerFechaAutomatico("PesajeFaeneo_automatico_2026-13-01.zip", out _));
        Assert.False(ReglasBackup.TryLeerFechaAutomatico("otro.zip", out _));
        Assert.False(ReglasBackup.TryLeerFechaAutomatico("PesajeFaeneo_manual_2026-10-01_100000.zip", out _));
    }

    [Fact]
    public void SeleccionarAutomaticosAEliminar_RespetaRetencionYMinimoConservados()
    {
        var hoy = new DateOnly(2026, 10, 1);
        // 40 días consecutivos hasta hoy
        var nombres = new List<string>();
        for (int i = 39; i >= 0; i--)
        {
            var f = hoy.AddDays(-i);
            nombres.Add($"PesajeFaeneo_automatico_{f:yyyy-MM-dd}.zip");
        }
        nombres.Add("otro.txt");
        nombres.Add("PesajeFaeneo_manual_2026-10-01_100000.zip");

        var aEliminar = ReglasBackup.SeleccionarAutomaticosAEliminar(nombres, 30, hoy);
        // Debe eliminar los 10 más viejos (días -39 a -30)
        Assert.Equal(10, aEliminar.Count);
        Assert.DoesNotContain("otro.txt", aEliminar);
        Assert.DoesNotContain("PesajeFaeneo_manual_2026-10-01_100000.zip", aEliminar);

        // Caso donde todos son viejos (> 60 días): debe conservar los 3 más recientes
        var viejos = new List<string>();
        for (int i = 100; i >= 61; i--)
        {
            var f = hoy.AddDays(-i);
            viejos.Add($"PesajeFaeneo_automatico_{f:yyyy-MM-dd}.zip");
        }
        var aEliminarViejos = ReglasBackup.SeleccionarAutomaticosAEliminar(viejos, 30, hoy);
        Assert.Equal(37, aEliminarViejos.Count); // 40 - 3 = 37 eliminados
    }

    [Fact]
    public void SeleccionarSeguridadAEliminar_ConservaDiezPorTipo()
    {
        var nombres = new List<string>();
        for (int i = 1; i <= 12; i++)
        {
            nombres.Add($"PesajeFaeneo_prerestauracion_2026-10-01_{i:D6}.zip");
        }
        for (int i = 1; i <= 2; i++)
        {
            nombres.Add($"PesajeFaeneo_preimportacion_2026-10-01_{i:D6}.zip");
        }

        var eliminadosRestauracion = ReglasBackup.SeleccionarSeguridadAEliminar(nombres, TipoBackup.PreRestauracion);
        Assert.Equal(2, eliminadosRestauracion.Count);
        Assert.Contains("PesajeFaeneo_prerestauracion_2026-10-01_000001.zip", eliminadosRestauracion);
        Assert.Contains("PesajeFaeneo_prerestauracion_2026-10-01_000002.zip", eliminadosRestauracion);

        var eliminadosImportacion = ReglasBackup.SeleccionarSeguridadAEliminar(nombres, TipoBackup.PreImportacion);
        Assert.Empty(eliminadosImportacion);
    }

    [Fact]
    public void ValidarConfiguracion_ValidaDiasYCarpetas()
    {
        var c6 = new ConfiguracionBackup(true, true, 6, null);
        Assert.NotEmpty(ReglasBackup.ValidarConfiguracion(c6));

        var c366 = new ConfiguracionBackup(true, true, 366, null);
        Assert.NotEmpty(ReglasBackup.ValidarConfiguracion(c366));

        var c7 = new ConfiguracionBackup(true, true, 7, null);
        Assert.Empty(ReglasBackup.ValidarConfiguracion(c7));

        var c365 = new ConfiguracionBackup(true, true, 365, null);
        Assert.Empty(ReglasBackup.ValidarConfiguracion(c365));

        var cRel = new ConfiguracionBackup(true, true, 30, "Backups");
        Assert.NotEmpty(ReglasBackup.ValidarConfiguracion(cRel));

        var cAbs = new ConfiguracionBackup(true, true, 30, @"C:\Backups");
        Assert.Empty(ReglasBackup.ValidarConfiguracion(cAbs));
    }
}
