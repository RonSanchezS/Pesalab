using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests.Application.Utilitarios.Importacion;

public class ConversoresImportacionTests
{
    private static readonly TimeZoneInfo ZonaUtcMenos4 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");

    [Theory]
    [InlineData("N° Res", "NRES")]
    [InlineData("Nro. Bol", "NROBOL")]
    [InlineData("Dirección", "DIRECCION")]
    [InlineData("  Marca / Ganadero  ", "MARCAGANADERO")]
    public void ClaveColumna_NormalizaCorrectamente(string entrada, string esperada)
    {
        Assert.Equal(esperada, ConversoresImportacion.ClaveColumna(entrada));
    }

    [Theory]
    [InlineData("123,5", 123.5)]
    [InlineData("123.5", 123.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1.234.567", 1234567)]
    [InlineData(" -5,25 ", -5.25)]
    [InlineData("0", 0)]
    public void TryDecimal_CasosValidos_DevuelveTrue(string entrada, decimal esperado)
    {
        Assert.True(ConversoresImportacion.TryDecimal(entrada, out var val));
        Assert.Equal(esperado, val);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3.4.5")]
    public void TryDecimal_CasosInvalidos_DevuelveFalse(string? entrada)
    {
        Assert.False(ConversoresImportacion.TryDecimal(entrada, out _));
    }

    [Fact]
    public void TryEntero_CasosMilesYDecimales_DevuelveEsperado()
    {
        Assert.True(ConversoresImportacion.TryEntero("1.234", out var v1));
        Assert.Equal(1234L, v1);

        // TryDecimal con 1.234 devuelve 1.234m
        Assert.True(ConversoresImportacion.TryDecimal("1.234", out var dec));
        Assert.Equal(1.234m, dec);

        Assert.True(ConversoresImportacion.TryEntero("123.0", out var v2));
        Assert.Equal(123L, v2);

        Assert.True(ConversoresImportacion.TryEntero("0010", out var v3));
        Assert.Equal(10L, v3);

        Assert.False(ConversoresImportacion.TryEntero("12.5", out _));
        Assert.False(ConversoresImportacion.TryEntero("abc", out _));
        Assert.False(ConversoresImportacion.TryEntero(null, out _));
    }

    [Fact]
    public void TryFechaHora_DiversosFormatosYCombinaciones()
    {
        // dd/MM/yyyy
        Assert.True(ConversoresImportacion.TryFechaHora("01/02/2020", null, out var f1));
        Assert.Equal(new DateTime(2020, 2, 1), f1);

        // Fecha + Hora
        Assert.True(ConversoresImportacion.TryFechaHora("2020-02-01", "07:08", out var f2));
        Assert.Equal(new DateTime(2020, 2, 1, 7, 8, 0), f2);

        // Fecha con hora sobreescrita por columna Hora
        Assert.True(ConversoresImportacion.TryFechaHora("2020-02-01 23:00:00", "07:08", out var f3));
        Assert.Equal(new DateTime(2020, 2, 1, 7, 8, 0), f3);

        // Hora desde Access (fecha base 1899-12-30)
        Assert.True(ConversoresImportacion.TryFechaHora("2020-02-01", "1899-12-30 07:08:00", out var f4));
        Assert.Equal(new DateTime(2020, 2, 1, 7, 8, 0), f4);

        // Formato yyyyMMdd
        Assert.True(ConversoresImportacion.TryFechaHora("20200201", null, out var f5));
        Assert.Equal(new DateTime(2020, 2, 1), f5);

        // Inválidos
        Assert.False(ConversoresImportacion.TryFechaHora("31/02/2020", null, out _));
        Assert.False(ConversoresImportacion.TryFechaHora("01/02/2020", "25:00", out _));
        Assert.False(ConversoresImportacion.TryFechaHora(null, null, out _));
    }

    [Fact]
    public void Booleano_DiversosValores()
    {
        Assert.True(ConversoresImportacion.Booleano("SI"));
        Assert.True(ConversoresImportacion.Booleano("1"));
        Assert.True(ConversoresImportacion.Booleano("TRUE"));
        Assert.True(ConversoresImportacion.Booleano("ACTIVO"));

        Assert.False(ConversoresImportacion.Booleano("NO"));
        Assert.False(ConversoresImportacion.Booleano("0"));
        Assert.False(ConversoresImportacion.Booleano("FALSE"));
        Assert.False(ConversoresImportacion.Booleano("INACTIVO"));

        Assert.Null(ConversoresImportacion.Booleano(null));
        Assert.Null(ConversoresImportacion.Booleano(""));
        Assert.Null(ConversoresImportacion.Booleano("quizá"));
    }

    [Fact]
    public void TryDesposte_ValoresValidosEInvalidos()
    {
        Assert.True(ConversoresImportacion.TryDesposte("", out var t1));
        Assert.Null(t1);

        Assert.True(ConversoresImportacion.TryDesposte("MEDIA RES", out var t2));
        Assert.Equal(TipoDesposte.MediaRes, t2);

        Assert.True(ConversoresImportacion.TryDesposte("1", out var t3));
        Assert.Equal(TipoDesposte.MediaRes, t3);

        Assert.True(ConversoresImportacion.TryDesposte("RC", out var t4));
        Assert.Equal(TipoDesposte.ResCompleta, t4);

        Assert.True(ConversoresImportacion.TryDesposte("ENTERA", out var t5));
        Assert.Equal(TipoDesposte.ResCompleta, t5);

        Assert.False(ConversoresImportacion.TryDesposte("DESCONOCIDO", out _));
    }

    [Fact]
    public void TrySexo_ValoresValidosEInvalidos()
    {
        Assert.True(ConversoresImportacion.TrySexo("", out var s1));
        Assert.Null(s1);

        Assert.True(ConversoresImportacion.TrySexo("MACHO", out var s2));
        Assert.Equal('M', s2);

        Assert.True(ConversoresImportacion.TrySexo("HEMBRA", out var s3));
        Assert.Equal('H', s3);

        Assert.True(ConversoresImportacion.TrySexo("F", out var s4));
        Assert.Equal('H', s4);

        Assert.False(ConversoresImportacion.TrySexo("OTRO", out _));
    }

    [Fact]
    public void TryEstado_ColumnaBooleanaYColumnaTexto()
    {
        // Columna booleana (ANULADA)
        Assert.True(ConversoresImportacion.TryEstado("1", columnaBooleana: true, out var e1));
        Assert.Equal(EstadoPesaje.Anulado, e1);

        Assert.True(ConversoresImportacion.TryEstado("0", columnaBooleana: true, out var e2));
        Assert.Equal(EstadoPesaje.Vigente, e2);

        // Columna texto (ESTADO)
        Assert.True(ConversoresImportacion.TryEstado("1", columnaBooleana: false, out var e3));
        Assert.Equal(EstadoPesaje.Vigente, e3);

        Assert.True(ConversoresImportacion.TryEstado("2", columnaBooleana: false, out var e4));
        Assert.Equal(EstadoPesaje.Anulado, e4);

        Assert.True(ConversoresImportacion.TryEstado("VIGENTE", columnaBooleana: false, out var e5));
        Assert.Equal(EstadoPesaje.Vigente, e5);

        Assert.True(ConversoresImportacion.TryEstado("ANULADA", columnaBooleana: false, out var e6));
        Assert.Equal(EstadoPesaje.Anulado, e6);

        Assert.False(ConversoresImportacion.TryEstado("OTRO", columnaBooleana: false, out _));
    }

    [Fact]
    public void LocalAUtc_ConvierteHoraLocalUtcMenosCuatroACorrespondienteUtc()
    {
        var local = new DateTime(2020, 2, 1, 22, 0, 0);
        var utc = ConversoresImportacion.LocalAUtc(local, ZonaUtcMenos4);

        Assert.Equal(new DateTime(2020, 2, 2, 2, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }
}
