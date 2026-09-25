using System.Text;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

public class SeparadorTramasTests
{
    [Fact]
    public void Agregar_DosTramasConCrLf_DevuelveDosTramas()
    {
        var separador = new SeparadorTramas();
        var bytes = Encoding.Latin1.GetBytes("12.3\r\n45.6\r\n");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Equal(2, tramas.Count);
        Assert.Equal("12.3", tramas[0]);
        Assert.Equal("45.6", tramas[1]);
        Assert.Equal(0, separador.TramasDescartadas);
    }

    [Fact]
    public void Agregar_EntradaFragmentada_DevuelveLasMismasTramas()
    {
        var separador = new SeparadorTramas();
        var chunk1 = Encoding.Latin1.GetBytes("12.");
        var chunk2 = Encoding.Latin1.GetBytes("3\r\n45");
        var chunk3 = Encoding.Latin1.GetBytes(".6\r\n");

        var tramas1 = separador.Agregar(chunk1, chunk1.Length);
        var tramas2 = separador.Agregar(chunk2, chunk2.Length);
        var tramas3 = separador.Agregar(chunk3, chunk3.Length);

        var total = new List<string>();
        total.AddRange(tramas1);
        total.AddRange(tramas2);
        total.AddRange(tramas3);

        Assert.Equal(2, total.Count);
        Assert.Equal("12.3", total[0]);
        Assert.Equal("45.6", total[1]);
        Assert.Equal(0, separador.TramasDescartadas);
    }

    [Fact]
    public void Agregar_SoloDelimitadores_NoEmiteTramasVacias()
    {
        var separador = new SeparadorTramas();
        var bytes = Encoding.Latin1.GetBytes("\r\n\r\n");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Empty(tramas);
        Assert.Equal(0, separador.TramasDescartadas);
    }

    [Fact]
    public void Agregar_ConStxYEtx_DescartaBasuraPreviaYEmiteTrama()
    {
        var separador = new SeparadorTramas();
        // "basura" + STX (0x02) + "12.3" + ETX (0x03)
        var bytes = Encoding.Latin1.GetBytes("basura\u000212.3\u0003");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Single(tramas);
        Assert.Equal("12.3", tramas[0]);
        Assert.Equal(0, separador.TramasDescartadas);
    }

    [Fact]
    public void Agregar_Supera256CaracteresSinDelimitador_DescartaEIncrementaContador()
    {
        var separador = new SeparadorTramas();
        var entradaLarga = new string('1', 300);
        var bytes1 = Encoding.Latin1.GetBytes(entradaLarga);
        var bytes2 = Encoding.Latin1.GetBytes("\r\n5.0\r\n");

        var tramas1 = separador.Agregar(bytes1, bytes1.Length);
        var tramas2 = separador.Agregar(bytes2, bytes2.Length);

        Assert.Empty(tramas1);
        Assert.Single(tramas2);
        Assert.Equal("5.0", tramas2[0]);
        Assert.Equal(1, separador.TramasDescartadas);
    }

    [Fact]
    public void Agregar_CaracteresLatin1Especiales_SePreservanCorrectamente()
    {
        var separador = new SeparadorTramas();
        // Byte 0xB0 es '°' en Latin1
        var bytes = new byte[] { 0xB0, 0x0D, 0x0A };

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Single(tramas);
        Assert.Equal("°", tramas[0]);
    }

    [Fact]
    public void Reiniciar_LimpiaAcumulador()
    {
        var separador = new SeparadorTramas();
        var chunk1 = Encoding.Latin1.GetBytes("incompleto");
        separador.Agregar(chunk1, chunk1.Length);

        separador.Reiniciar();

        var chunk2 = Encoding.Latin1.GetBytes("12.3\r\n");
        var tramas = separador.Agregar(chunk2, chunk2.Length);

        Assert.Single(tramas);
        Assert.Equal("12.3", tramas[0]);
    }

    [Fact]
    public void Agregar_DelimitadorEspacio_SeparaTramasRx8SinCrLf()
    {
        var separador = new SeparadorTramas(DelimitadorTrama.Espacio);
        var bytes = Encoding.Latin1.GetBytes("=000000 =012345 =012350 ");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Equal(new[] { "=000000", "=012345", "=012350" }, tramas);
    }

    [Fact]
    public void Agregar_DelimitadorEspacio_EspaciosConsecutivosNoGeneranTramasVacias_YCrLfSigueDelimitando()
    {
        var separador = new SeparadorTramas(DelimitadorTrama.Espacio);
        var bytes = Encoding.Latin1.GetBytes("=000001   =000002\r\n");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Equal(new[] { "=000001", "=000002" }, tramas);
    }

    [Fact]
    public void Agregar_DelimitadorFinDeLinea_ConservaLosEspaciosDentroDeLaTrama()
    {
        var separador = new SeparadorTramas(DelimitadorTrama.FinDeLinea);
        var bytes = Encoding.Latin1.GetBytes("ST,GS,+  98.6kg\r\n");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Equal("ST,GS,+  98.6kg", Assert.Single(tramas));
    }

    [Fact]
    public void Agregar_SignoIgual_RafagaRealSinEspacios_EntregaCadaLecturaPorSeparado()
    {
        // Capturado del RX8 Accurex en sitio: en ráfagas las tramas vienen unidas por '-' y sin espacio.
        var separador = new SeparadorTramas(DelimitadorTrama.SignoIgual);
        var bytes = Encoding.Latin1.GetBytes("=067000 =180000 =650000-=381000-=161000-=492000 =631000 ");

        var tramas = separador.Agregar(bytes, bytes.Length);

        Assert.Equal(new[] { "=067000", "=180000", "=650000-", "=381000-", "=161000-", "=492000", "=631000" }, tramas);
    }

    [Fact]
    public void Agregar_SignoIgual_LaTramaEnCursoSeEntregaAlLlegarElSiguienteIgual_AunqueVengaFragmentada()
    {
        var separador = new SeparadorTramas(DelimitadorTrama.SignoIgual);
        var parte1 = Encoding.Latin1.GetBytes("=6500");
        var parte2 = Encoding.Latin1.GetBytes("00-=38");

        Assert.Empty(separador.Agregar(parte1, parte1.Length));
        Assert.Equal("=650000-", Assert.Single(separador.Agregar(parte2, parte2.Length)));
    }

    [Fact]
    public void Agregar_Espacio_RafagaSinEspacios_SeUneEnUnaSolaTrama()
    {
        // Documenta por qué el RX8 necesita SignoIgual y no Espacio.
        var separador = new SeparadorTramas(DelimitadorTrama.Espacio);
        var bytes = Encoding.Latin1.GetBytes("=650000-=381000-=161000 ");

        Assert.Equal("=650000-=381000-=161000", Assert.Single(separador.Agregar(bytes, bytes.Length)));
    }

    [Fact]
    public void Agregar_DelimitadorFinDeLinea_SinCrLfNuncaEntregaTramas()
    {
        // Documenta por qué el RX8 requiere DelimitadorTrama.Espacio: con fin de línea no hay tramas.
        var separador = new SeparadorTramas();
        var bytes = Encoding.Latin1.GetBytes("=000000 =000000 =000000 ");

        Assert.Empty(separador.Agregar(bytes, bytes.Length));
    }
}
