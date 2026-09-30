using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Infrastructure.Archivos;
using PesajeFaeneo.Infrastructure.Common;
using Xunit;

namespace PesajeFaeneo.Tests;

public class AlmacenConfiguracionImpresionJsonTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppPaths _appPaths;
    private const string EquipoPrueba = "PC-PRUEBA";

    public AlmacenConfiguracionImpresionJsonTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "test_cfg_imp_" + Guid.NewGuid());
        _appPaths = new AppPaths(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Limpieza de mejor esfuerzo
            }
        }
    }

    private AlmacenConfiguracionImpresionJson CrearAlmacen(string nombreEquipo = EquipoPrueba)
    {
        return new AlmacenConfiguracionImpresionJson(
            _appPaths,
            NullLogger<AlmacenConfiguracionImpresionJson>.Instance,
            nombreEquipo);
    }

    [Fact]
    public async Task LeerAsync_SinArchivo_RetornaNull()
    {
        var almacen = CrearAlmacen();

        var config = await almacen.LeerAsync();

        Assert.Null(config);
    }

    [Fact]
    public async Task GuardarYLeer_IdaYVuelta_ConservaTodosLosCampos()
    {
        var almacen = CrearAlmacen();
        var original = new ConfiguracionImpresion(
            NombreImpresora: null,
            Formato: FormatoBoleta.Ticket80mm,
            NombrePapel: "80mm Roll",
            Copias: 2,
            ImprimirAlGrabar: true,
            DesplazamientoXMm: -2.5m,
            DesplazamientoYMm: 3.0m);

        await almacen.GuardarAsync(original);
        var leida = await almacen.LeerAsync();

        Assert.NotNull(leida);
        Assert.Null(leida.NombreImpresora);
        Assert.Equal(FormatoBoleta.Ticket80mm, leida.Formato);
        Assert.Equal("80mm Roll", leida.NombrePapel);
        Assert.Equal(2, leida.Copias);
        Assert.True(leida.ImprimirAlGrabar);
        Assert.Equal(-2.5m, leida.DesplazamientoXMm);
        Assert.Equal(3.0m, leida.DesplazamientoYMm);
    }

    [Fact]
    public async Task GuardarAsync_NoDejaArchivoTemporal()
    {
        var almacen = CrearAlmacen();
        var config = new ConfiguracionImpresion("HP", FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

        await almacen.GuardarAsync(config);

        var tmpPath = Path.Combine(_appPaths.ConfigDir, "impresion.json.tmp");
        Assert.False(File.Exists(tmpPath));
        var destPath = Path.Combine(_appPaths.ConfigDir, "impresion.json");
        Assert.True(File.Exists(destPath));
    }

    [Fact]
    public async Task LeerAsync_ArchivoDeOtroEquipo_RetornaNullSinLanzar()
    {
        Directory.CreateDirectory(_appPaths.ConfigDir);
        var json = """
        {
            "Version": 1,
            "NombreEquipo": "OTRA-PC",
            "NombreImpresora": "HP LaserJet",
            "Formato": "MediaCarta",
            "NombrePapel": null,
            "Copias": 1,
            "ImprimirAlGrabar": false,
            "DesplazamientoXMm": 0.0,
            "DesplazamientoYMm": 0.0
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_appPaths.ConfigDir, "impresion.json"), json);

        var almacen = CrearAlmacen(EquipoPrueba);
        var resultado = await almacen.LeerAsync();

        Assert.Null(resultado);
    }

    [Fact]
    public async Task LeerAsync_JsonDanado_RetornaNullSinLanzar()
    {
        Directory.CreateDirectory(_appPaths.ConfigDir);
        await File.WriteAllTextAsync(Path.Combine(_appPaths.ConfigDir, "impresion.json"), "{ no es json");

        var almacen = CrearAlmacen();
        var resultado = await almacen.LeerAsync();

        Assert.Null(resultado);
    }

    [Fact]
    public async Task LeerAsync_VersionIncompatible_RetornaNull()
    {
        Directory.CreateDirectory(_appPaths.ConfigDir);
        var json = """
        {
            "Version": 2,
            "NombreEquipo": "PC-PRUEBA",
            "NombreImpresora": null,
            "Formato": "MediaCarta",
            "NombrePapel": null,
            "Copias": 1,
            "ImprimirAlGrabar": false,
            "DesplazamientoXMm": 0.0,
            "DesplazamientoYMm": 0.0
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_appPaths.ConfigDir, "impresion.json"), json);

        var almacen = CrearAlmacen();
        var resultado = await almacen.LeerAsync();

        Assert.Null(resultado);
    }

    [Fact]
    public async Task LeerAsync_CopiasFueraDeRangoEnJson_RetornaNull()
    {
        Directory.CreateDirectory(_appPaths.ConfigDir);
        var json = """
        {
            "Version": 1,
            "NombreEquipo": "PC-PRUEBA",
            "NombreImpresora": null,
            "Formato": "MediaCarta",
            "NombrePapel": null,
            "Copias": 9,
            "ImprimirAlGrabar": false,
            "DesplazamientoXMm": 0.0,
            "DesplazamientoYMm": 0.0
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_appPaths.ConfigDir, "impresion.json"), json);

        var almacen = CrearAlmacen();
        var resultado = await almacen.LeerAsync();

        Assert.Null(resultado);
    }

    [Fact]
    public async Task LeerAsync_FormatoDesconocidoEnJson_RetornaNullSinLanzar()
    {
        Directory.CreateDirectory(_appPaths.ConfigDir);
        var json = """
        {
            "Version": 1,
            "NombreEquipo": "PC-PRUEBA",
            "NombreImpresora": null,
            "Formato": "Desconocido",
            "NombrePapel": null,
            "Copias": 1,
            "ImprimirAlGrabar": false,
            "DesplazamientoXMm": 0.0,
            "DesplazamientoYMm": 0.0
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_appPaths.ConfigDir, "impresion.json"), json);

        var almacen = CrearAlmacen();
        var resultado = await almacen.LeerAsync();

        Assert.Null(resultado);
    }

    [Fact]
    public void Constructor_NoCreaDirectorioConfig()
    {
        Assert.False(Directory.Exists(_appPaths.ConfigDir));

        var almacen = CrearAlmacen();

        Assert.NotNull(almacen);
        Assert.False(Directory.Exists(_appPaths.ConfigDir));
    }
}
