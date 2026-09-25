using System.Drawing;
using System.Globalization;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.WinForms.Infraestructura;

public static class PresentacionBalanza
{
    public static readonly Color ColorVerdeOscuro = Color.DarkGreen;
    public static readonly Color ColorRojoOscuro = Color.DarkRed;
    public static readonly Color ColorGris = Color.Gray;
    public static readonly Color ColorFondoSimulador = Color.FromArgb(255, 238, 204); // Naranja claro

    public static string TextoEstado(InstantaneaBalanza i)
    {
        ArgumentNullException.ThrowIfNull(i);

        string prefijo = i.EsSimulador ? "[SIMULADOR] " : string.Empty;

        string cuerpo = i.Estado switch
        {
            EstadoBalanza.Detenida => "Balanza: detenida",
            EstadoBalanza.Conectando => string.IsNullOrWhiteSpace(i.Mensaje) ? "Balanza: conectando…" : i.Mensaje,
            EstadoBalanza.Conectada => string.IsNullOrWhiteSpace(i.Descripcion)
                ? "Balanza: conectada"
                : $"Balanza: conectada ({i.Descripcion})",
            EstadoBalanza.SinDatos => "Balanza: sin datos",
            EstadoBalanza.TramasNoReconocidas => "Balanza: tramas no reconocidas",
            EstadoBalanza.Error => $"Balanza: error — {i.Mensaje}",
            EstadoBalanza.EnDiagnostico => "Balanza: en diagnóstico",
            _ => "Balanza: desconocida"
        };

        return prefijo + cuerpo;
    }

    public static string TextoPeso(InstantaneaBalanza i)
    {
        ArgumentNullException.ThrowIfNull(i);

        if (!i.PesoKg.HasValue)
        {
            return "---- kg";
        }

        return $"{i.PesoKg.Value.ToString("0.0", CultureInfo.CurrentCulture)} kg";
    }

    public static Color ColorEstado(InstantaneaBalanza i)
    {
        ArgumentNullException.ThrowIfNull(i);

        if (i.Estado == EstadoBalanza.Conectada)
        {
            return i.Estable ? ColorVerdeOscuro : SystemColors.ControlText;
        }

        if (i.Estado is EstadoBalanza.Error or EstadoBalanza.SinDatos or EstadoBalanza.TramasNoReconocidas)
        {
            return ColorRojoOscuro;
        }

        return ColorGris;
    }
}
