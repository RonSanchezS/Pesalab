using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class ConversorValoresOrigenTests
{
    [Fact]
    public void ATexto_NullYDbNull_DevuelveNull()
    {
        Assert.Null(ConversorValoresOrigen.ATexto(null));
        Assert.Null(ConversorValoresOrigen.ATexto(DBNull.Value));
    }

    [Fact]
    public void ATexto_String_RecortaYVacioDevuelveNull()
    {
        Assert.Equal("Hola", ConversorValoresOrigen.ATexto("  Hola  "));
        Assert.Null(ConversorValoresOrigen.ATexto("   "));
        Assert.Null(ConversorValoresOrigen.ATexto(""));
    }

    [Fact]
    public void ATexto_DateTime_SinHoraFormateaFechaConHoraFormateaFechaHora()
    {
        var soloFecha = new DateTime(2020, 2, 1);
        Assert.Equal("2020-02-01", ConversorValoresOrigen.ATexto(soloFecha));

        var conHora = new DateTime(2020, 2, 1, 7, 8, 9);
        Assert.Equal("2020-02-01 07:08:09", ConversorValoresOrigen.ATexto(conHora));
    }

    [Fact]
    public void ATexto_Bool_DevuelveTrueOFalseEnMinusculas()
    {
        Assert.Equal("true", ConversorValoresOrigen.ATexto(true));
        Assert.Equal("false", ConversorValoresOrigen.ATexto(false));
    }

    [Fact]
    public void ATexto_NumericosEnterosYDecimal_DevuelveInvariante()
    {
        Assert.Equal("123.45", ConversorValoresOrigen.ATexto(123.45m));
        Assert.Equal("42", ConversorValoresOrigen.ATexto(42));
        Assert.Equal("10000000000", ConversorValoresOrigen.ATexto(10000000000L));
        Assert.Equal("12", ConversorValoresOrigen.ATexto((short)12));
        Assert.Equal("255", ConversorValoresOrigen.ATexto((byte)255));
    }

    [Fact]
    public void ATexto_DoubleYFloat_DevuelveFormatoRInvariante()
    {
        Assert.Equal("250.75", ConversorValoresOrigen.ATexto(250.75d));
        Assert.Equal("12.5", ConversorValoresOrigen.ATexto(12.5f));
    }

    [Fact]
    public void ATexto_ByteArray_DevuelveNull()
    {
        Assert.Null(ConversorValoresOrigen.ATexto(new byte[] { 1, 2, 3 }));
    }
}
