using PesajeFaeneo.Application.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ReglasLicenciaTests
{
    private const string Hwid = "e704a377c9e29b967b41c7c8c793d1d0e1c9ee287d0082abab837cdb463dc5aa";
    private const long Inicio = 1_700_000_000;
    private const long Dia = ReglasLicencia.SegundosDia;

    private static DatosLicenciaLocal Prueba(long inicio, long ultimo, string hwid = Hwid) =>
        new(inicio.ToString(), ultimo.ToString(), FirmaLicencia.Prueba(inicio, ultimo, hwid), null);

    // ---------------------------------------------------------------- FirmaLicencia (vectores conocidos)

    [Fact]
    public void FirmaLicencia_Hwid_VectorConocido()
    {
        Assert.Equal(Hwid, FirmaLicencia.Hwid("11111111-2222-3333-4444-555555555555"));
    }

    [Fact]
    public void FirmaLicencia_Hwid_IgnoraMayusculasYEspacios()
    {
        Assert.Equal(FirmaLicencia.Hwid("abcdef01-0000-0000-0000-000000000000"), FirmaLicencia.Hwid("  ABCDEF01-0000-0000-0000-000000000000 "));
        Assert.Equal("0f9836e0403210d20c70aa8d584c7fba8bc9c880db911488297bcdc1f86b1809", FirmaLicencia.Hwid("abcdef01-0000-0000-0000-000000000000"));
    }

    [Fact]
    public void FirmaLicencia_Prueba_VectorConocido()
    {
        Assert.Equal("fbd1c740e5bc1ffcd3b5e2ced3a6c3e60c78b74d45090343688955ec0994347d", FirmaLicencia.Prueba(1_700_000_000, 1_700_003_600, Hwid));
    }

    [Fact]
    public void FirmaLicencia_TokenActivacion_VectorConocido()
    {
        Assert.Equal("92a1fe07c5838b47addc6bbbcdb1e1afd8db6d15d200f9902383f68737a32073", FirmaLicencia.TokenActivacion(Hwid));
    }

    [Fact]
    public void FirmaLicencia_SalEsLaDePesajeFaeneoYLaSalidaEsHexMinusculas()
    {
        Assert.Equal("pf-86372349-fb7c-4def-8add-f9b6b87ad2c2", FirmaLicencia.Sal);
        var firma = FirmaLicencia.Prueba(1, 2, Hwid);
        Assert.Equal(64, firma.Length);
        Assert.Equal(firma.ToLowerInvariant(), firma);
        Assert.NotEqual(FirmaLicencia.Prueba(1, 2, Hwid), FirmaLicencia.Prueba(2, 1, Hwid));
    }

    // ---------------------------------------------------------------- Calcular: todas las ramas de D3

    [Fact]
    public void Calcular_ConTokenValido_EstaActivada_AunqueHayaPruebaAlterada()
    {
        var datos = new DatosLicenciaLocal("basura", null, null, FirmaLicencia.TokenActivacion(Hwid));

        var r = ReglasLicencia.Calcular(datos, Inicio, Hwid);

        Assert.Equal(EstadoLicencia.Activada, r.Estado);
        Assert.False(r.ReescribirUltimo);
    }

    [Fact]
    public void Calcular_ConTokenDeOtroEquipo_NoEstaActivada()
    {
        var datos = new DatosLicenciaLocal(null, null, null, FirmaLicencia.TokenActivacion("otro-equipo-0000000000000000"));

        Assert.Equal(EstadoLicencia.NoIniciada, ReglasLicencia.Calcular(datos, Inicio, Hwid).Estado);
    }

    [Fact]
    public void Calcular_ConTokenVacio_NoEstaActivada()
    {
        var datos = new DatosLicenciaLocal(null, null, null, string.Empty);

        Assert.Equal(EstadoLicencia.NoIniciada, ReglasLicencia.Calcular(datos, Inicio, Hwid).Estado);
    }

    [Fact]
    public void Calcular_SinDatos_EsNoIniciada()
    {
        var r = ReglasLicencia.Calcular(DatosLicenciaLocal.Vacio, Inicio, Hwid);

        Assert.Equal(TipoEstadoLicencia.NoIniciada, r.Estado.Tipo);
        Assert.False(r.Estado.PermiteOperar);
    }

    [Theory]
    [InlineData("1700000000", "1700000000", "firma-incorrecta")]
    [InlineData("1700000000", null, null)]
    [InlineData("1700000000", "1700000000", null)]
    [InlineData("no-es-numero", "1700000000", "x")]
    [InlineData("1700000000", "no-es-numero", "x")]
    [InlineData("", "", "")]
    public void Calcular_ConInicioPeroDatosInvalidos_EstaAlterada(string? d, string? l, string? s)
    {
        var r = ReglasLicencia.Calcular(new DatosLicenciaLocal(d, l, s, null), Inicio, Hwid);

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.Alterada), r.Estado);
        Assert.False(r.ReescribirUltimo);
    }

    [Fact]
    public void Calcular_FirmaDeOtroEquipo_EstaAlterada()
    {
        var datos = Prueba(Inicio, Inicio, "hwid-de-otro-equipo-xxxxxxxxxxxx");

        Assert.Equal(MotivoVencimiento.Alterada, ReglasLicencia.Calcular(datos, Inicio, Hwid).Estado.Motivo);
    }

    [Fact]
    public void Calcular_RecienIniciada_Tiene15Dias()
    {
        var r = ReglasLicencia.Calcular(Prueba(Inicio, Inicio), Inicio, Hwid);

        Assert.Equal(EstadoLicencia.EnPrueba(15), r.Estado);
        Assert.True(r.Estado.PermiteOperar);
        Assert.False(r.ReescribirUltimo);
    }

    [Theory]
    [InlineData(0L, 15)]
    [InlineData(Dia - 1, 15)]
    [InlineData(Dia, 14)]
    [InlineData(7 * Dia + 5, 8)]
    [InlineData(14 * Dia, 1)]
    [InlineData(15 * Dia - 1, 1)]
    public void Calcular_DiasRestantes_SonQuinceMenosDiasCompletos(long transcurridos, int esperados)
    {
        // l = inicio: el instante actual manda (efectivo = ahora).
        var r = ReglasLicencia.Calcular(Prueba(Inicio, Inicio), Inicio + transcurridos, Hwid);

        Assert.Equal(TipoEstadoLicencia.Prueba, r.Estado.Tipo);
        Assert.Equal(esperados, r.Estado.DiasRestantes);
    }

    [Theory]
    [InlineData(15 * Dia)]
    [InlineData(15 * Dia + 1)]
    [InlineData(40 * Dia)]
    public void Calcular_ConQuinceDiasOMas_VenceLaPrueba(long transcurridos)
    {
        var r = ReglasLicencia.Calcular(Prueba(Inicio, Inicio), Inicio + transcurridos, Hwid);

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada), r.Estado);
        Assert.False(r.Estado.PermiteOperar);
    }

    [Fact]
    public void Calcular_RelojAtrasadoMasDeUnDia_VencePorReloj()
    {
        var ultimo = Inicio + 3 * Dia;

        var r = ReglasLicencia.Calcular(Prueba(Inicio, ultimo), ultimo - Dia - 1, Hwid);

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.Reloj), r.Estado);
        Assert.False(r.ReescribirUltimo);
    }

    [Fact]
    public void Calcular_RelojAtrasadoExactamenteUnDia_SeTolera_YUsaElUltimoVisto()
    {
        var ultimo = Inicio + 3 * Dia;

        var r = ReglasLicencia.Calcular(Prueba(Inicio, ultimo), ultimo - Dia, Hwid);

        // efectivo = max(ahora, l) = l: pasaron 3 días completos desde el inicio.
        Assert.Equal(EstadoLicencia.EnPrueba(12), r.Estado);
        Assert.False(r.ReescribirUltimo);
        Assert.Equal(ultimo, r.Ultimo);
    }

    [Fact]
    public void Calcular_RelojAdelantado_Hace15DiasVence_YEmpujaElUltimoVisto()
    {
        var r = ReglasLicencia.Calcular(Prueba(Inicio, Inicio), Inicio + 16 * Dia, Hwid);

        Assert.Equal(MotivoVencimiento.PruebaTerminada, r.Estado.Motivo);
        // Igual que compute_status: el último visto se reescribe aunque la prueba ya esté vencida.
        Assert.True(r.ReescribirUltimo);
        Assert.Equal(Inicio + 16 * Dia, r.Ultimo);
        Assert.Equal(Inicio, r.Inicio);
    }

    [Theory]
    [InlineData(3_599L, false)]
    [InlineData(3_600L, true)]
    [InlineData(3_601L, true)]
    public void Calcular_ReescribeElUltimoVistoSoloSiAvanzoUnaHoraOMas(long avance, bool reescribe)
    {
        var r = ReglasLicencia.Calcular(Prueba(Inicio, Inicio), Inicio + avance, Hwid);

        Assert.Equal(reescribe, r.ReescribirUltimo);
        Assert.Equal(Inicio + avance, r.Ultimo);
    }

    [Fact]
    public void Calcular_AhoraMenorQueUltimoDentroDeTolerancia_NoReescribe()
    {
        var ultimo = Inicio + Dia;

        var r = ReglasLicencia.Calcular(Prueba(Inicio, ultimo), ultimo - 10, Hwid);

        Assert.False(r.ReescribirUltimo);
        Assert.Equal(ultimo, r.Ultimo);
    }

    // ---------------------------------------------------------------- Texto

    [Theory]
    [InlineData(TipoEstadoLicencia.Activada, 0, null, "Activada")]
    [InlineData(TipoEstadoLicencia.Prueba, 15, null, "Prueba (15 días restantes)")]
    [InlineData(TipoEstadoLicencia.Prueba, 1, null, "Prueba (1 día restante)")]
    [InlineData(TipoEstadoLicencia.NoIniciada, 0, null, "Sin activar")]
    [InlineData(TipoEstadoLicencia.Vencida, 0, MotivoVencimiento.PruebaTerminada, "Modo consulta (prueba terminada)")]
    [InlineData(TipoEstadoLicencia.Vencida, 0, MotivoVencimiento.Reloj, "Modo consulta (cambio en el reloj del sistema)")]
    [InlineData(TipoEstadoLicencia.Vencida, 0, MotivoVencimiento.Alterada, "Modo consulta (datos de la prueba alterados)")]
    public void EstadoLicencia_Texto(TipoEstadoLicencia tipo, int dias, MotivoVencimiento? motivo, string esperado)
    {
        Assert.Equal(esperado, new EstadoLicencia(tipo, dias, motivo).Texto);
    }

    [Fact]
    public void EstadoLicencia_PermiteOperar_SoloActivadaOPrueba()
    {
        Assert.True(EstadoLicencia.Activada.PermiteOperar);
        Assert.True(EstadoLicencia.EnPrueba(3).PermiteOperar);
        Assert.False(EstadoLicencia.NoIniciada.PermiteOperar);
        Assert.False(EstadoLicencia.VencidaPor(MotivoVencimiento.Reloj).PermiteOperar);
    }

    // ---------------------------------------------------------------- Clave

    [Theory]
    [InlineData("0123-4567-89AB-CDEF-0123", true)]
    [InlineData("0123456789abcdef0123", true)]
    [InlineData("  0123 4567 89ab cdef 0123  ", true)]
    [InlineData("0123_4567/89AB.CDEF-0123", true)]
    [InlineData("0123-4567-89AB-CDEF-012", false)]
    [InlineData("0123-4567-89AB-CDEF-01234", false)]
    [InlineData("0123-4567-89AB-CDEF-012G", false)]
    [InlineData("ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ClaveConFormatoValido(string? clave, bool esperado)
    {
        Assert.Equal(esperado, ReglasLicencia.ClaveConFormatoValido(clave));
    }

    [Fact]
    public void NormalizarClave_QuitaNoAlfanumericosYPasaAMayusculas()
    {
        Assert.Equal("0123456789ABCDEF0123", ReglasLicencia.NormalizarClave(" 0123-4567-89ab-cdef-0123 "));
        Assert.Equal(string.Empty, ReglasLicencia.NormalizarClave(null));
    }

    [Fact]
    public void FormatearClave_PoneGuionesCadaCuatro()
    {
        Assert.Equal("0123-4567-89AB-CDEF-0123", ReglasLicencia.FormatearClave("0123456789abcdef0123"));
    }
}
