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
    bool Ocupado,
    bool DesposteFijo = false,    // F11: el tipo lo fija la configuración (sin combo en pantalla)
    bool FleteroSinElegir = false); // F12 (E1): texto en Fletero que no quedó elegido

internal sealed record EstadoUiPesaje(
    bool PuedeCapturarIzquierdo,
    bool PuedeCapturarDerecho,
    bool PuedeBorrarPesaje,
    bool PuedeGrabar,
    string Mensaje,
    NivelMensajeGuia Nivel);

internal static class EvaluadorPantallaPesaje
{
    private static readonly CultureInfo CulturaBo = CultureInfo.GetCultureInfo("es-BO");

    // F12 (E1): lo usan también la edición de boleta (FrmEditarBoleta) y los manuales.
    internal const string MensajeFleteroSinElegir = "Elija el fletero de la lista o borre el texto.";

    public static EstadoUiPesaje Evaluar(DatosPantallaPesaje d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var puedeCapturarIzq = !d.Ocupado && d.Capturas.Evaluar(LadoPesaje.Izquierdo, d.TipoDesposte, d.Instantanea, d.Ahora) == MotivoRechazoCaptura.Ninguno;
        var puedeCapturarDer = !d.Ocupado && d.Capturas.Evaluar(LadoPesaje.Derecho, d.TipoDesposte, d.Instantanea, d.Ahora) == MotivoRechazoCaptura.Ninguno;
        var puedeBorrarPesaje = !d.Ocupado && d.Capturas.TienePesos;

        var erroresPesos = ReglasPesaje.ValidarPesos(
            d.TipoDesposte,
            d.Capturas.PesoIzquierdo ?? 0m,
            d.Capturas.PesoDerecho ?? 0m);

        var pesosCompletos = erroresPesos.Count == 0;

        var puedeGrabar = !d.Ocupado &&
                          d.GanaderoSeleccionado &&
                          d.ClienteSeleccionado &&
                          !d.FleteroSinElegir &&
                          pesosCompletos &&
                          !(d.Capturas.CapturadoConSimulador && !d.PuedeUsarSimulador);

        var (mensaje, nivel) = CalcularMensaje(d, pesosCompletos, erroresPesos);

        return new EstadoUiPesaje(
            puedeCapturarIzq,
            puedeCapturarDer,
            puedeBorrarPesaje,
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
            return ("Seleccione el ganadero (código o nombre).", NivelMensajeGuia.Info);
        }

        // 4. !ClienteSeleccionado
        if (!d.ClienteSeleccionado)
        {
            return ("Seleccione el cliente (código o nombre).", NivelMensajeGuia.Info);
        }

        // 4b. F12 (E1): fletero escrito sin elegir (bloquea el grabado)
        if (d.FleteroSinElegir)
        {
            return (MensajeFleteroSinElegir, NivelMensajeGuia.Advertencia);
        }

        // 5. Pesos completos y válidos
        if (pesosCompletos)
        {
            return ("Listo para grabar (F12).", NivelMensajeGuia.Exito);
        }

        // 5b. F12 (E4): los dos lados capturados pero incoherentes (p. ej. media res): la única salida es F8,
        // así que va antes del retorno a cero, la estabilidad y el rango.
        if (erroresPesos.Count > 0 && d.Capturas.PesoIzquierdo.HasValue && d.Capturas.PesoDerecho.HasValue)
        {
            return ($"{erroresPesos[0].Mensaje} Use F8 para borrar el pesaje.", NivelMensajeGuia.Advertencia);
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

        // 8. Peso estable fuera de rango (F12, E4: la balanza en reposo, hasta el umbral de retorno a cero,
        // no es "fuera de rango": cae en los mensajes de captura de abajo)
        if (d.Instantanea.Estado == EstadoBalanza.Conectada &&
            d.Instantanea.Estable &&
            d.Instantanea.PesoKg.HasValue &&
            Math.Abs(d.Instantanea.PesoKg.Value) > ReglasPesaje.UmbralRetornoCeroKg &&
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
            return (d.DesposteFijo ? "Capture un lado (F5 o F6)." : "Media res: capture un lado (F5 o F6).", NivelMensajeGuia.Info);
        }

        // 11. Pesos incoherentes (p. ej. media res con dos lados)
        if (erroresPesos.Count > 0)
        {
            return ($"{erroresPesos[0].Mensaje} Use F8 para borrar el pesaje.", NivelMensajeGuia.Advertencia);
        }

        return (string.Empty, NivelMensajeGuia.Info);
    }
}
