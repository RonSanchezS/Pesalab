using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Presentacion.Pesaje;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ControlCapturasTests
{
    private static InstantaneaBalanza CrearInstantanea(
        decimal? pesoKg = 120.0m,
        bool estable = true,
        EstadoBalanza estado = EstadoBalanza.Conectada,
        bool esSimulador = false,
        DateTimeOffset? instante = null,
        int timeoutMs = 3000)
    {
        return new InstantaneaBalanza(
            1,
            estado,
            pesoKg,
            estable,
            instante ?? DateTimeOffset.UtcNow,
            null,
            "Conectada",
            esSimulador)
        {
            TimeoutMs = timeoutMs
        };
    }

    [Fact]
    public void Evaluar_SimuladorNoPermitido_DevuelveSimuladorNoPermitido()
    {
        var control = new ControlCapturas(puedeUsarSimulador: false);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(esSimulador: true, instante: ahora);

        var motivo = control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);

        Assert.Equal(MotivoRechazoCaptura.SimuladorNoPermitido, motivo);
    }

    [Fact]
    public void Evaluar_BalanzaNoCapturable_PorDesconexionOInestableOLecturaVieja()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;

        // Desconectada (Detenida)
        var instDesc = CrearInstantanea(estado: EstadoBalanza.Detenida, instante: ahora);
        Assert.Equal(MotivoRechazoCaptura.BalanzaNoCapturable, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instDesc, ahora));

        // Inestable
        var instInest = CrearInstantanea(estable: false, instante: ahora);
        Assert.Equal(MotivoRechazoCaptura.BalanzaNoCapturable, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instInest, ahora));

        // Sin peso
        var instSinPeso = CrearInstantanea(pesoKg: null, instante: ahora);
        Assert.Equal(MotivoRechazoCaptura.BalanzaNoCapturable, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instSinPeso, ahora));

        // Lectura vieja: 3001 ms con TimeoutMs = 3000
        var instVieja = CrearInstantanea(instante: ahora.AddMilliseconds(-3001), timeoutMs: 3000);
        Assert.Equal(MotivoRechazoCaptura.BalanzaNoCapturable, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instVieja, ahora));
    }

    [Fact]
    public void Evaluar_PesoFueraDeRango_MenorA10OMayorA600()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;

        // 9.94m redondea a 9.9m (< 10.0m)
        var instBajo = CrearInstantanea(pesoKg: 9.94m, instante: ahora);
        Assert.Equal(MotivoRechazoCaptura.PesoFueraDeRango, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instBajo, ahora));

        // 600.06m redondea a 600.1m (> 600.0m)
        var instAlto = CrearInstantanea(pesoKg: 600.06m, instante: ahora);
        Assert.Equal(MotivoRechazoCaptura.PesoFueraDeRango, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instAlto, ahora));
    }

    [Fact]
    public void Capturar_IzquierdoYLuegoDerechoSinPasarPorCero_RechazaPorEsperandoRetornoACero()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120.0m, instante: ahora);

        var resIzq = control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        Assert.Equal(MotivoRechazoCaptura.Ninguno, resIzq);
        Assert.True(control.EsperandoRetornoACero);

        // Intentar derecho inmediatamente sin pasar por cero
        var resDer = control.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, inst, ahora);
        Assert.Equal(MotivoRechazoCaptura.EsperandoRetornoACero, resDer);
    }

    [Fact]
    public void Observar_ConectadaConMenorAUmbral_LevantaEsperandoRetornoACero()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var instPeso = CrearInstantanea(pesoKg: 120.0m, instante: ahora);

        control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, instPeso, ahora);
        Assert.True(control.EsperandoRetornoACero);

        // Observar con 1.5 kg pero Estado == SinDatos -> no se levanta
        control.Observar(CrearInstantanea(pesoKg: 1.5m, estado: EstadoBalanza.SinDatos, instante: ahora));
        Assert.True(control.EsperandoRetornoACero);

        // Observar con 1.5 kg y Estado == Conectada -> se levanta
        control.Observar(CrearInstantanea(pesoKg: 1.5m, estado: EstadoBalanza.Conectada, instante: ahora));
        Assert.False(control.EsperandoRetornoACero);

        // Ahora derecho es permitido
        var resDer = control.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, instPeso, ahora);
        Assert.Equal(MotivoRechazoCaptura.Ninguno, resDer);
    }

    [Fact]
    public void Evaluar_MediaRes_ConOtroLadoCapturado_RechazaLadoNoPermitido()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120.0m, instante: ahora);

        control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.MediaRes, inst, ahora);
        control.Observar(CrearInstantanea(pesoKg: 0.5m, estado: EstadoBalanza.Conectada, instante: ahora));

        // Derecho debe dar LadoNoPermitido
        Assert.Equal(MotivoRechazoCaptura.LadoNoPermitido, control.Evaluar(LadoPesaje.Derecho, TipoDesposte.MediaRes, inst, ahora));

        // Izquierdo de nuevo está permitido (sobrescribe)
        Assert.Equal(MotivoRechazoCaptura.Ninguno, control.Evaluar(LadoPesaje.Izquierdo, TipoDesposte.MediaRes, inst, ahora));
    }

    [Fact]
    public void BorrarPesaje_LimpiaPesosYLevantaRetornoACero()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120.0m, esSimulador: true, instante: ahora);

        control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        Assert.True(control.TienePesos);
        Assert.True(control.CapturadoConSimulador);
        Assert.True(control.EsperandoRetornoACero);

        control.BorrarPesaje();

        Assert.False(control.TienePesos);
        Assert.Null(control.PesoIzquierdo);
        Assert.Null(control.PesoDerecho);
        Assert.False(control.CapturadoConSimulador);
        Assert.False(control.EsperandoRetornoACero);
    }

    [Fact]
    public void ReiniciarTrasGrabar_LimpiaPesosPeroConservaEsperandoRetornoACero()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120.0m, esSimulador: true, instante: ahora);

        control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        Assert.True(control.EsperandoRetornoACero);

        control.ReiniciarTrasGrabar();

        Assert.False(control.TienePesos);
        Assert.Null(control.PesoIzquierdo);
        Assert.Null(control.PesoDerecho);
        Assert.False(control.CapturadoConSimulador);
        Assert.True(control.EsperandoRetornoACero); // Conservado
    }

    [Fact]
    public void Capturar_RedondeaAPesoConUnDecimal()
    {
        var control = new ControlCapturas(puedeUsarSimulador: true);
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 123.45m, instante: ahora);

        var res = control.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);

        Assert.Equal(MotivoRechazoCaptura.Ninguno, res);
        Assert.Equal(123.5m, control.PesoIzquierdo);
    }
}
