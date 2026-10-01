using System.Text;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class DetectorCodificacionTests
{
    static DetectorCodificacionTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Detectar_ConBomUtf8_DevuelveUtf8ConSaltoTres()
    {
        var texto = "MARCA;NOMBRE\n";
        var bytesTexto = Encoding.UTF8.GetBytes(texto);
        var datos = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(bytesTexto).ToArray();

        var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, null);

        Assert.Equal(Encoding.UTF8, cod);
        Assert.Equal(3, salto);
        Assert.Equal("UTF-8", nombre);
    }

    [Fact]
    public void Detectar_ConBomUtf16Le_DevuelveUnicodeConSaltoDos()
    {
        var texto = "A";
        var bytesTexto = Encoding.Unicode.GetBytes(texto);
        var datos = new byte[] { 0xFF, 0xFE }.Concat(bytesTexto).ToArray();

        var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, null);

        Assert.Equal(Encoding.Unicode, cod);
        Assert.Equal(2, salto);
        Assert.Equal("UTF-16", nombre);
    }

    [Fact]
    public void Detectar_Utf8SinBomConPena_DetectaUtf8()
    {
        var datos = Encoding.UTF8.GetBytes("PEÑA");

        var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, null);

        Assert.Equal(Encoding.UTF8, cod);
        Assert.Equal(0, salto);
        Assert.Equal("UTF-8", nombre);
        Assert.Equal("PEÑA", cod.GetString(datos, salto, datos.Length - salto));
    }

    [Fact]
    public void Detectar_Bytes1252ConPena_DetectaWindows1252()
    {
        var cod1252 = Encoding.GetEncoding(1252);
        var datos = cod1252.GetBytes("PEÑA");

        var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, null);

        Assert.Equal(1252, cod.CodePage);
        Assert.Equal(0, salto);
        Assert.Equal("Windows-1252", nombre);
        Assert.Equal("PEÑA", cod.GetString(datos, salto, datos.Length - salto));
    }

    [Fact]
    public void Detectar_Forzada850ConBytes850DePena_Devuelve850YDecodificaCorrectamente()
    {
        var cod850 = Encoding.GetEncoding(850);
        var datos = cod850.GetBytes("PEÑA");

        var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, 850);

        Assert.Equal(850, cod.CodePage);
        Assert.Equal(0, salto);
        Assert.Equal("DOS 850", nombre);
        Assert.Equal("PEÑA", cod.GetString(datos, salto, datos.Length - salto));
    }

    [Fact]
    public void Detectar_PaginaNoPermitida_LanzaArgumentOutOfRangeException()
    {
        var datos = new byte[] { (byte)'A', (byte)'B' };

        Assert.Throws<ArgumentOutOfRangeException>(() => DetectorCodificacion.Detectar(datos, 1234));
    }
}
