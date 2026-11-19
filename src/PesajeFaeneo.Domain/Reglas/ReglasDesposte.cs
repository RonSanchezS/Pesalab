using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Domain.Reglas;

/// <summary>
/// F11: tipo de pesaje de la empresa (res completa, media res o ambos). Decide qué tipo de desposte se graba y cuándo
/// se muestra el desposte en pantallas, boletas y reportes.
/// </summary>
public static class ReglasDesposte
{
    /// <summary>D2: un modo sin definir (instalación nueva sin terminar el asistente) funciona como Ambos.</summary>
    public static ModoDesposte Efectivo(ModoDesposte? modo) => modo ?? ModoDesposte.Ambos;

    public static bool EsValido(ModoDesposte modo) =>
        modo is ModoDesposte.SoloMediaRes or ModoDesposte.SoloResCompleta or ModoDesposte.Ambos;

    /// <summary>El único tipo que se pesa con el modo, o null si se elige en cada pesaje.</summary>
    public static TipoDesposte? TipoUnico(ModoDesposte modo) => modo switch
    {
        ModoDesposte.SoloResCompleta => TipoDesposte.ResCompleta,
        ModoDesposte.SoloMediaRes => TipoDesposte.MediaRes,
        _ => null
    };

    public static bool Permite(ModoDesposte modo, TipoDesposte tipo) =>
        TipoUnico(modo) is not { } unico || unico == tipo;

    /// <summary>
    /// D6 ("ocultar, nunca falsear"): el desposte se muestra con el modo Ambos o cuando entre los datos mostrados hay
    /// algún tipo distinto del único que pesa la empresa (boletas anteriores al cambio de modo o importadas).
    /// </summary>
    public static bool MostrarDesposte(ModoDesposte modo, IEnumerable<TipoDesposte> presentes)
    {
        ArgumentNullException.ThrowIfNull(presentes);

        if (TipoUnico(modo) is not { } unico)
        {
            return true;
        }

        return presentes.Any(t => t != unico);
    }

    public static bool MostrarDesposte(ModoDesposte modo, TipoDesposte tipo) =>
        MostrarDesposte(modo, new[] { tipo });

    public static string Texto(ModoDesposte? modo) => modo switch
    {
        null => "Sin definir",
        ModoDesposte.SoloResCompleta => "Solo res completa",
        ModoDesposte.SoloMediaRes => "Solo media res",
        ModoDesposte.Ambos => "Res completa y media res",
        _ => ((int)modo.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)
    };
}
