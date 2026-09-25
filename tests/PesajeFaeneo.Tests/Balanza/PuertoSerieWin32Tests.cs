using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using Xunit;
using Dcb = PesajeFaeneo.Infrastructure.Balanza.PuertoSerieWin32.Dcb;
using FormatoSerie = PesajeFaeneo.Infrastructure.Balanza.PuertoSerieWin32.FormatoSerie;

namespace PesajeFaeneo.Tests.Balanza;

public class PuertoSerieWin32Tests
{
    // DCB tal como lo reporta el driver CH340 real (sonda en COM4): 9600 8N1, flags 0x1001.
    private static Dcb DcbDriverCh340() => new()
    {
        BaudRate = 9600,
        Flags = 0x1001,
        XonLim = 2048,
        XoffLim = 512,
        ByteSize = 8,
        Parity = 0,
        StopBits = 0
    };

    private static readonly FormatoSerie Formato9600_8N1 = new(9600, 8, 0, 0);

    [Fact]
    public void AjustarFormato_DriverYaEnElFormatoDeseado_NoLlamaSetCommState()
    {
        // CH340 rechaza cualquier SetCommState con error 31 aunque no haya cambios: no debe llamarse.
        int escrituras = 0;
        PuertoSerieWin32.AjustarFormato(
            "COM4",
            Formato9600_8N1,
            (out Dcb dcb) => { dcb = DcbDriverCh340(); return true; },
            (ref Dcb dcb, out int error) => { escrituras++; error = 31; return false; });

        Assert.Equal(0, escrituras);
    }

    [Fact]
    public void AjustarFormato_FormatoDistinto_SoloCambiaBaudiosYFormato_PreservandoFlagsDelDriver()
    {
        Dcb? escrito = null;
        PuertoSerieWin32.AjustarFormato(
            "COM4",
            new FormatoSerie(19200, 7, 2, 2),
            (out Dcb dcb) => { dcb = DcbDriverCh340(); return true; },
            (ref Dcb dcb, out int error) => { escrito = dcb; error = 0; return true; });

        Assert.NotNull(escrito);
        Assert.Equal(19200u, escrito.Value.BaudRate);
        Assert.Equal((byte)7, escrito.Value.ByteSize);
        Assert.Equal((byte)2, escrito.Value.Parity);
        Assert.Equal((byte)2, escrito.Value.StopBits);
        Assert.Equal(0x1001u, escrito.Value.Flags);
        Assert.Equal((ushort)2048, escrito.Value.XonLim);
        Assert.Equal((ushort)512, escrito.Value.XoffLim);
    }

    [Fact]
    public void AjustarFormato_DriverRechazaPeroElFormatoQuedoAplicado_SeAcepta()
    {
        int lecturas = 0;
        PuertoSerieWin32.AjustarFormato(
            "COM4",
            new FormatoSerie(19200, 8, 0, 0),
            (out Dcb dcb) =>
            {
                lecturas++;
                dcb = DcbDriverCh340();
                if (lecturas > 1) dcb.BaudRate = 19200;
                return true;
            },
            (ref Dcb dcb, out int error) => { error = 31; return false; });

        Assert.Equal(2, lecturas);
    }

    [Fact]
    public void AjustarFormato_DriverRechazaYElFormatoNoCambio_LanzaConMensajeAccionable()
    {
        var ex = Assert.Throws<ConexionBalanzaException>(() => PuertoSerieWin32.AjustarFormato(
            "COM4",
            new FormatoSerie(19200, 8, 0, 0),
            (out Dcb dcb) => { dcb = DcbDriverCh340(); return true; },
            (ref Dcb dcb, out int error) => { error = 31; return false; }));

        Assert.Contains("COM4", ex.Message);
        Assert.Contains("19200 baudios 8N1", ex.Message);
        Assert.Contains("9600 baudios 8N1", ex.Message);
        Assert.Contains("error 31", ex.Message);
    }

    [Fact]
    public void AjustarFormato_NoSePuedeLeerElDcb_LanzaIOException()
    {
        Assert.Throws<IOException>(() => PuertoSerieWin32.AjustarFormato(
            "COM4",
            Formato9600_8N1,
            (out Dcb dcb) => { dcb = default; return false; },
            (ref Dcb dcb, out int error) => { error = 0; return true; }));
    }

    [Fact]
    public void MapeoEnumsADcb_CubreTodosLosValoresValidosEInvalidos()
    {
        Assert.Equal(0, PuertoSerieWin32.MapearParidad(ParidadSerial.None));
        Assert.Equal(1, PuertoSerieWin32.MapearParidad(ParidadSerial.Odd));
        Assert.Equal(2, PuertoSerieWin32.MapearParidad(ParidadSerial.Even));
        Assert.Equal(3, PuertoSerieWin32.MapearParidad(ParidadSerial.Mark));
        Assert.Equal(4, PuertoSerieWin32.MapearParidad(ParidadSerial.Space));
        Assert.Throws<ArgumentOutOfRangeException>(() => PuertoSerieWin32.MapearParidad((ParidadSerial)999));

        // Valores DCB de Win32: ONESTOPBIT = 0, ONE5STOPBITS = 1, TWOSTOPBITS = 2.
        Assert.Equal(0, PuertoSerieWin32.MapearBitsParada(BitsParada.One));
        Assert.Equal(1, PuertoSerieWin32.MapearBitsParada(BitsParada.OnePointFive));
        Assert.Equal(2, PuertoSerieWin32.MapearBitsParada(BitsParada.Two));
        Assert.Throws<ArgumentOutOfRangeException>(() => PuertoSerieWin32.MapearBitsParada((BitsParada)999));
    }

    [Fact]
    public void Abrir_PuertoInexistente_LanzaIOExceptionTraducibleAMensajeDeCable()
    {
        var config = new PesajeFaeneo.Application.Balanza.Dtos.ConfiguracionBalanzaDto(
            TipoConexion.Serial, "COM999", 9600, 8, ParidadSerial.None, BitsParada.One,
            "127.0.0.1", 4001, @"(?<peso>\d+)", 5, 0.5m, 3000);
        using var puerto = new PuertoSerieWin32(config);

        Assert.Throws<IOException>(() => puerto.Abrir());
    }
}
