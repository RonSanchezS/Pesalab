using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorConfiguracionBalanzaTests
{
    private static ConfiguracionBalanzaDto CrearValida() => new(
        TipoConexion: TipoConexion.Simulador,
        PuertoCom: "COM1",
        BaudRate: 9600,
        DataBits: 8,
        Paridad: ParidadSerial.None,
        BitsParada: BitsParada.One,
        HostTcp: "192.168.1.100",
        PuertoTcp: 4001,
        PatronRegex: @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)",
        LecturasParaEstable: 5,
        ToleranciaKg: 0.5m,
        TimeoutMs: 3000);

    [Fact]
    public void Validar_ConfiguracionSembrada_EsValida()
    {
        var dto = CrearValida();
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        Assert.Empty(errores);
    }

    [Fact]
    public void Validar_TipoConexionInvalido_RetornaError()
    {
        var dto = CrearValida() with { TipoConexion = (TipoConexion)99 };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.TipoConexion, err.Campo);
        Assert.Equal("Seleccione el tipo de conexión.", err.Mensaje);
    }

    [Theory]
    [InlineData("COM0")]
    [InlineData("COM1000")]
    [InlineData("LPT1")]
    [InlineData("")]
    [InlineData("COM")]
    public void Validar_PuertoComInvalido_RetornaError(string puerto)
    {
        var dto = CrearValida() with { PuertoCom = puerto };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.PuertoCom, err.Campo);
        Assert.Equal("Ingrese un puerto válido (COM1 a COM999).", err.Mensaje);
    }

    [Fact]
    public void Validar_BaudRateInvalido_RetornaError()
    {
        var dto = CrearValida() with { BaudRate = 100 };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.BaudRate, err.Campo);
        Assert.Equal("Seleccione una velocidad válida.", err.Mensaje);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    public void Validar_DataBitsInvalido_RetornaError(int dataBits)
    {
        var dto = CrearValida() with { DataBits = dataBits };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.DataBits, err.Campo);
        Assert.Equal("Los bits de datos deben ser 7 u 8.", err.Mensaje);
    }

    [Fact]
    public void Validar_ParidadInvalida_RetornaError()
    {
        var dto = CrearValida() with { Paridad = (ParidadSerial)99 };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.Paridad, err.Campo);
        Assert.Equal("Seleccione la paridad.", err.Mensaje);
    }

    [Fact]
    public void Validar_BitsParadaInvalido_RetornaError()
    {
        // BitsParada.OnePointFive no está en BitsParadaPermitidos (solo One y Two)
        var dto = CrearValida() with { BitsParada = BitsParada.OnePointFive };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.BitsParada, err.Campo);
        Assert.Equal("Los bits de parada deben ser 1 o 2.", err.Mensaje);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("con espacios")]
    public void Validar_HostTcpInvalido_RetornaError(string host)
    {
        var dto = CrearValida() with { HostTcp = host };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.HostTcp, err.Campo);
        Assert.Equal("Ingrese una dirección IP o nombre de equipo válido.", err.Mensaje);
    }

    [Fact]
    public void Validar_HostTcpExcede100Caracteres_RetornaError()
    {
        var dto = CrearValida() with { HostTcp = new string('a', 101) };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.HostTcp, err.Campo);
        Assert.Equal("Ingrese una dirección IP o nombre de equipo válido.", err.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validar_PuertoTcpInvalido_RetornaError(int puerto)
    {
        var dto = CrearValida() with { PuertoTcp = puerto };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.PuertoTcp, err.Campo);
        Assert.Equal("El puerto TCP debe estar entre 1 y 65535.", err.Mensaje);
    }

    [Fact]
    public void Validar_PatronRegexInvalido_RetornaError()
    {
        var dto = CrearValida() with { PatronRegex = "" };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.PatronRegex, err.Campo);
        Assert.Equal("Ingrese el patrón de lectura.", err.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Validar_LecturasParaEstableInvalido_RetornaError(int lecturas)
    {
        var dto = CrearValida() with { LecturasParaEstable = lecturas };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.LecturasParaEstable, err.Campo);
        Assert.Equal("Las lecturas para estabilidad deben estar entre 1 y 50.", err.Mensaje);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    [InlineData(0.25)] // Más de 1 decimal
    public void Validar_ToleranciaKgInvalida_RetornaError(double tolerancia)
    {
        var dto = CrearValida() with { ToleranciaKg = (decimal)tolerancia };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.ToleranciaKg, err.Campo);
        Assert.Equal("La tolerancia debe estar entre 0,0 y 10,0 kg (un decimal).", err.Mensaje);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(60001)]
    public void Validar_TimeoutMsInvalido_RetornaError(int timeout)
    {
        var dto = CrearValida() with { TimeoutMs = timeout };
        var errores = ValidadorConfiguracionBalanza.Validar(dto);
        var err = Assert.Single(errores);
        Assert.Equal(CamposBalanza.TimeoutMs, err.Campo);
        Assert.Equal("El tiempo sin datos debe estar entre 500 y 60000 ms.", err.Mensaje);
    }

    [Theory]
    [InlineData("COM1")]
    [InlineData("COM999")]
    public void Validar_PuertoComLimitesValidos_SinErrores(string puerto)
    {
        var dto = CrearValida() with { PuertoCom = puerto };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void Validar_PuertoTcpLimitesValidos_SinErrores(int puerto)
    {
        var dto = CrearValida() with { PuertoTcp = puerto };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Validar_LecturasParaEstableLimitesValidos_SinErrores(int lecturas)
    {
        var dto = CrearValida() with { LecturasParaEstable = lecturas };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(10.0)]
    public void Validar_ToleranciaKgLimitesValidos_SinErrores(double tolerancia)
    {
        var dto = CrearValida() with { ToleranciaKg = (decimal)tolerancia };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(60000)]
    public void Validar_TimeoutMsLimitesValidos_SinErrores(int timeout)
    {
        var dto = CrearValida() with { TimeoutMs = timeout };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("192.168.1.100")]
    [InlineData("::1")]
    public void Validar_HostTcpFormatosValidos_SinErrores(string host)
    {
        var dto = CrearValida() with { HostTcp = host };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }

    [Fact]
    public void Normalizar_LimpiaPuertoComYHost_YNoModificaPatronRegex()
    {
        var dto = CrearValida() with
        {
            PuertoCom = " com3 ",
            HostTcp = " 192.168.1.50 ",
            PatronRegex = " (?<peso>\\d+) "
        };

        var normalizado = ValidadorConfiguracionBalanza.Normalizar(dto);

        Assert.Equal("COM3", normalizado.PuertoCom);
        Assert.Equal("192.168.1.50", normalizado.HostTcp);
        Assert.Equal(" (?<peso>\\d+) ", normalizado.PatronRegex);
    }

    [Fact]
    public void Validar_DelimitadorTramaInvalido_RetornaError()
    {
        var dto = CrearValida() with { DelimitadorTrama = (DelimitadorTrama)99 };
        var err = Assert.Single(ValidadorConfiguracionBalanza.Validar(dto));
        Assert.Equal(CamposBalanza.DelimitadorTrama, err.Campo);
    }

    [Fact]
    public void Validar_UnidadPesoInvalida_RetornaError()
    {
        var dto = CrearValida() with { UnidadPeso = (UnidadPesoTrama)0 };
        var err = Assert.Single(ValidadorConfiguracionBalanza.Validar(dto));
        Assert.Equal(CamposBalanza.UnidadPeso, err.Campo);
    }

    [Fact]
    public void Validar_ConfiguracionRx8_EspacioYGramos_EsValida()
    {
        var dto = CrearValida() with
        {
            TipoConexion = TipoConexion.Serial,
            PuertoCom = "COM4",
            DelimitadorTrama = DelimitadorTrama.Espacio,
            UnidadPeso = UnidadPesoTrama.Gramos
        };
        Assert.Empty(ValidadorConfiguracionBalanza.Validar(dto));
    }
}
