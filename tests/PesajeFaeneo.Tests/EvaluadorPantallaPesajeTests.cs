using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Presentacion.Pesaje;
using Xunit;

namespace PesajeFaeneo.Tests;

public class EvaluadorPantallaPesajeTests
{
    private static InstantaneaBalanza CrearInstantanea(
        decimal? pesoKg = 120.0m,
        bool estable = true,
        EstadoBalanza estado = EstadoBalanza.Conectada,
        bool esSimulador = false,
        DateTimeOffset? instante = null)
    {
        return new InstantaneaBalanza(
            1,
            estado,
            pesoKg,
            estable,
            instante ?? DateTimeOffset.UtcNow,
            null,
            "Conectada",
            esSimulador);
    }

    [Fact]
    public void Fila1_SimuladorSinPermiso_MensajeModoSimuladorError()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(esSimulador: true, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: false);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: false,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Balanza en MODO SIMULADOR: solo un administrador puede capturar y grabar.", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Error, res.Nivel);
        Assert.False(res.PuedeCapturarIzquierdo);
        Assert.False(res.PuedeCapturarDerecho);
        Assert.False(res.PuedeGrabar);
    }

    [Fact]
    public void Fila2_BalanzaNoConectadaYSinPesosCompletos_MensajeErrorNoSePuedeCapturar()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(estado: EstadoBalanza.SinDatos, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Balanza: sin datos. No se puede capturar.", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Error, res.Nivel);
        Assert.False(res.PuedeCapturarIzquierdo);
        Assert.False(res.PuedeCapturarDerecho);
    }

    [Fact]
    public void Fila3_SinGanaderoSeleccionado_MensajeInfoSeleccioneGanadero()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: false,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Seleccione el ganadero (marca o nombre).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Info, res.Nivel);
        Assert.False(res.PuedeGrabar);
    }

    [Fact]
    public void Fila4_SinClienteSeleccionado_MensajeInfoSeleccioneCliente()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: false,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Seleccione el cliente (código o nombre).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Info, res.Nivel);
        Assert.False(res.PuedeGrabar);
    }

    [Fact]
    public void Fila5_PesosCompletosYValidos_MensajeExitoListoParaGrabar()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);
        capturas.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        capturas.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));
        capturas.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, CrearInstantanea(pesoKg: 118m, instante: ahora), ahora);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Listo para grabar (F12).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Exito, res.Nivel);
        Assert.True(res.PuedeGrabar);
        Assert.True(res.PuedeBorrarPesos);
    }

    [Fact]
    public void Fila6_EsperandoRetornoACero_MensajeAdvertenciaRetirePeso()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);
        capturas.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Retire el peso de la balanza (esperando retorno a cero).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Advertencia, res.Nivel);
        Assert.False(res.PuedeGrabar);
    }

    [Fact]
    public void Fila7_ConectadaPeroNoEstable_MensajeInfoEsperandoPesoEstable()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, estable: false, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Esperando peso estable…", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Info, res.Nivel);
    }

    [Fact]
    public void Fila8_PesoEstableFueraDeRango_MensajeAdvertenciaPesoFueraDeRango()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 5m, estable: true, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Peso fuera de rango (10,0 a 600,0 kg).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Advertencia, res.Nivel);
    }

    [Fact]
    public void Fila9_ResCompleta_FaltanLados_MuestraMensajeSegunLadoFaltante()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);

        // Faltan ambos
        var capturasVacias = new ControlCapturas(puedeUsarSimulador: true);
        var datosAmbos = new DatosPantallaPesaje(inst, ahora, true, true, TipoDesposte.ResCompleta, capturasVacias, true, false);
        Assert.Equal("Capture el lado izquierdo (F5) y el derecho (F6).", EvaluadorPantallaPesaje.Evaluar(datosAmbos).Mensaje);

        // Falta derecho (izquierdo capturado y ya pasó por cero)
        var capturasSoloIzq = new ControlCapturas(puedeUsarSimulador: true);
        capturasSoloIzq.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        capturasSoloIzq.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));
        var datosFaltaDer = new DatosPantallaPesaje(inst, ahora, true, true, TipoDesposte.ResCompleta, capturasSoloIzq, true, false);
        Assert.Equal("Capture el lado derecho (F6).", EvaluadorPantallaPesaje.Evaluar(datosFaltaDer).Mensaje);

        // Falta izquierdo (derecho capturado y ya pasó por cero)
        var capturasSoloDer = new ControlCapturas(puedeUsarSimulador: true);
        capturasSoloDer.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, inst, ahora);
        capturasSoloDer.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));
        var datosFaltaIzq = new DatosPantallaPesaje(inst, ahora, true, true, TipoDesposte.ResCompleta, capturasSoloDer, true, false);
        Assert.Equal("Capture el lado izquierdo (F5).", EvaluadorPantallaPesaje.Evaluar(datosFaltaIzq).Mensaje);
    }

    [Fact]
    public void Fila10_MediaResSinPeso_MensajeInfoMediaResCaptureUnLado()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.MediaRes,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Media res: capture un lado (F5 o F6).", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Info, res.Nivel);
    }

    [Fact]
    public void Fila11_PesosIncoherentes_MediaResConDosLados_MensajeAdvertenciaUseF8()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);
        // Capturar dos lados en res completa y luego cambiar tipo desposte a media res
        capturas.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        capturas.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));
        capturas.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, inst, ahora);
        capturas.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.MediaRes,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: false);

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.Equal("Media res: capture solo un lado. Use F8 para borrar.", res.Mensaje);
        Assert.Equal(NivelMensajeGuia.Advertencia, res.Nivel);
        Assert.False(res.PuedeGrabar);
    }

    [Fact]
    public void Ocupado_DeshabilitaTodasLasAcciones()
    {
        var ahora = DateTimeOffset.UtcNow;
        var inst = CrearInstantanea(pesoKg: 120m, instante: ahora);
        var capturas = new ControlCapturas(puedeUsarSimulador: true);
        capturas.Capturar(LadoPesaje.Izquierdo, TipoDesposte.ResCompleta, inst, ahora);
        capturas.Observar(CrearInstantanea(pesoKg: 0m, instante: ahora));
        capturas.Capturar(LadoPesaje.Derecho, TipoDesposte.ResCompleta, inst, ahora);

        var datos = new DatosPantallaPesaje(
            Instantanea: inst,
            Ahora: ahora,
            GanaderoSeleccionado: true,
            ClienteSeleccionado: true,
            TipoDesposte: TipoDesposte.ResCompleta,
            Capturas: capturas,
            PuedeUsarSimulador: true,
            Ocupado: true); // Ocupado

        var res = EvaluadorPantallaPesaje.Evaluar(datos);

        Assert.False(res.PuedeCapturarIzquierdo);
        Assert.False(res.PuedeCapturarDerecho);
        Assert.False(res.PuedeBorrarPesos);
        Assert.False(res.PuedeGrabar);
    }
}
