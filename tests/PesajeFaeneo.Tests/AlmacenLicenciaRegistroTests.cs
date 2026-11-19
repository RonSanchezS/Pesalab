using Microsoft.Win32;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Infrastructure.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>Usa una subclave temporal bajo HKCU\Software\PesajeFaeneoTests\{guid}; nunca toca la clave real de la licencia.</summary>
public sealed class AlmacenLicenciaRegistroTests : IDisposable
{
    private const string Raiz = @"Software\PesajeFaeneoTests";

    private readonly string _subclave = $@"{Raiz}\{Guid.NewGuid():N}\Cfg";
    private readonly string _subclaveRaizPrueba;

    public AlmacenLicenciaRegistroTests()
    {
        _subclaveRaizPrueba = _subclave[..^"\\Cfg".Length];
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_subclaveRaizPrueba, throwOnMissingSubKey: false);
        // Si no queda ninguna otra subclave de pruebas, se quita también la raíz.
        using var raiz = Registry.CurrentUser.OpenSubKey(Raiz);
        if (raiz is not null && raiz.SubKeyCount == 0)
        {
            raiz.Close();
            Registry.CurrentUser.DeleteSubKeyTree(Raiz, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Leer_SinLaSubclave_DevuelveTodoNulo()
    {
        var almacen = new AlmacenLicenciaRegistro(_subclave);

        Assert.Equal(DatosLicenciaLocal.Vacio, almacen.Leer());
        Assert.Null(Registry.CurrentUser.OpenSubKey(_subclave));
    }

    [Fact]
    public void EscribirPrueba_GuardaDLYSComoRegSzEnMinusculasHex()
    {
        var almacen = new AlmacenLicenciaRegistro(_subclave);
        var firma = FirmaLicencia.Prueba(1_700_000_000, 1_700_003_600, "hwid");

        almacen.EscribirPrueba(1_700_000_000, 1_700_003_600, firma);

        using var clave = Registry.CurrentUser.OpenSubKey(_subclave)!;
        Assert.Equal("1700000000", clave.GetValue("d"));
        Assert.Equal("1700003600", clave.GetValue("l"));
        Assert.Equal(firma, clave.GetValue("s"));
        Assert.Equal(RegistryValueKind.String, clave.GetValueKind("d"));
        Assert.Equal(RegistryValueKind.String, clave.GetValueKind("l"));
        Assert.Equal(RegistryValueKind.String, clave.GetValueKind("s"));
        Assert.Null(clave.GetValue("a"));
    }

    [Fact]
    public void EscribirYLeer_HacenIdaYVuelta()
    {
        var almacen = new AlmacenLicenciaRegistro(_subclave);
        var token = FirmaLicencia.TokenActivacion("hwid");

        almacen.EscribirPrueba(10, 20, "firma");
        almacen.EscribirActivacion(token);

        Assert.Equal(new DatosLicenciaLocal("10", "20", "firma", token), almacen.Leer());
    }

    [Fact]
    public void LimpiarActivacion_DejaElValorVacio_QueNoCuentaComoActivada()
    {
        var almacen = new AlmacenLicenciaRegistro(_subclave);
        almacen.EscribirActivacion(FirmaLicencia.TokenActivacion("hwid"));

        almacen.LimpiarActivacion();

        var datos = almacen.Leer();
        Assert.Equal(string.Empty, datos.Activacion);
        Assert.False(ReglasLicencia.EstaActivada(datos, "hwid"));
    }

    [Fact]
    public void EscribirActivacion_NoPisaLaPrueba()
    {
        var almacen = new AlmacenLicenciaRegistro(_subclave);
        almacen.EscribirPrueba(10, 20, "firma");

        almacen.EscribirActivacion("token");

        var datos = almacen.Leer();
        Assert.Equal("10", datos.Inicio);
        Assert.Equal("token", datos.Activacion);
    }

    [Fact]
    public void Leer_UnValorQueNoEsRegSz_SeTrataComoAusente()
    {
        using (var clave = Registry.CurrentUser.CreateSubKey(_subclave))
        {
            clave.SetValue("d", 1_700_000_000, RegistryValueKind.DWord);
            clave.SetValue("l", "20", RegistryValueKind.String);
        }

        var datos = new AlmacenLicenciaRegistro(_subclave).Leer();

        Assert.Null(datos.Inicio);
        Assert.Equal("20", datos.Ultimo);
    }

    [Fact]
    public void SubclaveVacia_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new AlmacenLicenciaRegistro(" "));
    }

    [Fact]
    public void IdentificadorEquipoWindows_DevuelveUnHwidSha256EstableEnHex()
    {
        var a = new IdentificadorEquipoWindows().Hwid;
        var b = new IdentificadorEquipoWindows().Hwid;

        Assert.Equal(64, a.Length);
        Assert.Equal(a.ToLowerInvariant(), a);
        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void IdentificadorEquipoWindows_LeeElMachineGuidDeWindows()
    {
        var guid = IdentificadorEquipoWindows.LeerMachineGuid();

        Assert.False(string.IsNullOrWhiteSpace(guid));
        Assert.Equal(FirmaLicencia.Hwid(guid), new IdentificadorEquipoWindows().Hwid);
    }
}
