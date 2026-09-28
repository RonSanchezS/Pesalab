using System.Globalization;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Presentacion.Pesaje;

internal enum NivelMensajeGuia { Info, Exito, Advertencia, Error }

internal sealed record DatosPantallaPesaje(
    InstantaneaBalanza Instantanea,
    DateTimeOffset Ahora,
    bool GanaderoSeleccionado,
    bool ClienteSeleccionado,
    TipoDesposte TipoDesposte,
    ControlCapturas Capturas,
    bool PuedeUsarSimulador,
    bool Ocupado);

internal sealed record EstadoUiPesaje(
    bool PuedeCapturarIzquierdo,
    bool PuedeCapturarDerecho,
    bool PuedeBorrarPesos,
    bool PuedeGrabar,
    string Mensaje,
    NivelMensajeGuia Nivel);

internal static class EvaluadorPantallaPesaje
{
    private static readonly CultureInfo CulturaBo = CultureInfo.GetCultureInfo("es-BO");

    public static EstadoUiPesaje Evaluar(DatosPantallaPesaje d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var puedeCapturarIzq = !d.Ocupado && d.Capturas.Evaluar(LadoPesaje.Izquierdo, d.TipoDesposte, d.Instantanea, d.Ahora) == MotivoRechazoCaptura.Ninguno;
        var puedeCapturarDer = !d.Ocupado && d.Capturas.Evaluar(LadoPesaje.Derecho, d.TipoDesposte, d.Instantanea, d.Ahora) == MotivoRechazoCaptura.Ninguno;
        var puedeBorrarPesos = !d.Ocupado && d.Capturas.TienePesos;

        var erroresPesos = ReglasPesaje.ValidarPesos(
            d.TipoDesposte,
            d.Capturas.PesoIzquierdo ?? 0m,
            d.Capturas.PesoDerecho ?? 0m);

        var pesosCompletos = erroresPesos.Count == 0;

        var puedeGrabar = !d.Ocupado &&
                          d.GanaderoSeleccionado &&
                          d.ClienteSeleccionado &&
                          pesosCompletos &&
                          !(d.Capturas.CapturadoConSimulador && !d.PuedeUsarSimulador);

        var (mensaje, nivel) = CalcularMensaje(d, pesosCompletos, erroresPesos);

        return new EstadoUiPesaje(
            puedeCapturarIzq,
            puedeCapturarDer,
            puedeBorrarPesos,
            puedeGrabar,
            mensaje,
            nivel);
    }

    private static (string Mensaje, NivelMensajeGuia Nivel) CalcularMensaje(
        DatosPantallaPesaje d,
        bool pesosCompletos,
        IReadOnlyList<ErrorValidacion> erroresPesos)
    {
        // 1. Instantanea.EsSimulador && !PuedeUsarSimulador
        if (d.Instantanea.EsSimulador && !d.PuedeUsarSimulador)
        {
            return ("Balanza en MODO SIMULADOR: solo un administrador puede capturar y grabar.", NivelMensajeGuia.Error);
        }

        // 2. Estado != Conectada y sin pesos completos
        if (d.Instantanea.Estado != EstadoBalanza.Conectada && !pesosCompletos)
        {
            return ($"{PresentacionBalanza.TextoEstado(d.Instantanea)}. No se puede capturar.", NivelMensajeGuia.Error);
        }

        // 3. !GanaderoSeleccionado
        if (!d.GanaderoSeleccionado)
        {
            return ("Seleccione el ganadero (marca o nombre).", NivelMensajeGuia.Info);
        }

        // 4. !ClienteSeleccionado
        if (!d.ClienteSeleccionado)
        {
            return ("Seleccione el cliente (código o nombre).", NivelMensajeGuia.Info);
        }

        // 5. Pesos completos y válidos
        if (pesosCompletos)
        {
            return ("Listo para grabar (F12).", NivelMensajeGuia.Exito);
        }

        // 6. Capturas.EsperandoRetornoACero
        if (d.Capturas.EsperandoRetornoACero)
        {
            return ("Retire el peso de la balanza (esperando retorno a cero).", NivelMensajeGuia.Advertencia);
        }

        // 7. Conectada pero no estable
        if (d.Instantanea.Estado == EstadoBalanza.Conectada && !d.Instantanea.Estable)
        {
            return ("Esperando peso estable…", NivelMensajeGuia.Info);
        }

        // 8. Peso estable fuera de rango
        if (d.Instantanea.Estado == EstadoBalanza.Conectada &&
            d.Instantanea.Estable &&
            d.Instantanea.PesoKg.HasValue &&
            !ReglasPesaje.EsPesoLadoValido(ReglasPesaje.Redondear(d.Instantanea.PesoKg.Value)))
        {
            var minStr = ReglasPesaje.PesoMinimoLadoKg.ToString("0.0", CulturaBo);
            var maxStr = ReglasPesaje.PesoMaximoLadoKg.ToString("0.0", CulturaBo);
            return ($"Peso fuera de rango ({minStr} a {maxStr} kg).", NivelMensajeGuia.Advertencia);
        }

        // 9. Res completa, faltan lados
        if (d.TipoDesposte == TipoDesposte.ResCompleta)
        {
            if (!d.Capturas.PesoIzquierdo.HasValue && !d.Capturas.PesoDerecho.HasValue)
            {
                return ("Capture el lado izquierdo (F5) y el derecho (F6).", NivelMensajeGuia.Info);
            }
            if (d.Capturas.PesoIzquierdo.HasValue && !d.Capturas.PesoDerecho.HasValue)
            {
                return ("Capture el lado derecho (F6).", NivelMensajeGuia.Info);
            }
            if (!d.Capturas.PesoIzquierdo.HasValue && d.Capturas.PesoDerecho.HasValue)
            {
                return ("Capture el lado izquierdo (F5).", NivelMensajeGuia.Info);
            }
        }

        // 10. Media res sin peso
        if (d.TipoDesposte == TipoDesposte.MediaRes &&
            !d.Capturas.PesoIzquierdo.HasValue &&
            !d.Capturas.PesoDerecho.HasValue)
        {
            return ("Media res: capture un lado (F5 o F6).", NivelMensajeGuia.Info);
        }

        // 11. Pesos incoherentes (p. ej. media res con dos lados)
        if (erroresPesos.Count > 0)
        {
            return ($"{erroresPesos[0].Mensaje} Use F8 para borrar.", NivelMensajeGuia.Advertencia);
        }

        return (string.Empty, NivelMensajeGuia.Info);
    }
}
