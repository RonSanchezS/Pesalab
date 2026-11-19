using System.Globalization;
using System.Text;

namespace PesajeFaeneo.Application.Licencias;

/// <summary>Valores crudos guardados en el almacén local (<c>d</c>, <c>l</c>, <c>s</c>, <c>a</c>); null = no existe.</summary>
public sealed record DatosLicenciaLocal(string? Inicio, string? Ultimo, string? Firma, string? Activacion)
{
    public static DatosLicenciaLocal Vacio { get; } = new(null, null, null, null);
}

public enum SituacionPrueba
{
    Ninguna,
    Alterada,
    Valida
}

/// <summary>Resultado de leer la prueba local: inicio y última vez visto en epoch (solo con <see cref="SituacionPrueba.Valida"/>).</summary>
public readonly record struct PruebaLocal(SituacionPrueba Situacion, long Inicio, long Ultimo);

/// <summary>
/// Resultado de <see cref="ReglasLicencia.Calcular"/>. Con <see cref="ReescribirUltimo"/> el llamador debe guardar la prueba
/// con <see cref="Inicio"/> y <see cref="Ultimo"/> (el último visto avanzó al menos una hora).
/// </summary>
public readonly record struct CalculoLicencia(EstadoLicencia Estado, bool ReescribirUltimo, long Inicio, long Ultimo);

/// <summary>F13 (D3, D9): reglas puras de la licencia, sin E/S ni reloj propio.</summary>
public static class ReglasLicencia
{
    public const int DiasPrueba = 15;
    public const long SegundosDia = 86_400;
    /// <summary>Un reloj atrasado hasta este margen se tolera; más allá la prueba vence por reloj.</summary>
    public const long ToleranciaRelojSegundos = SegundosDia;
    /// <summary>La última vez visto solo se reescribe cuando avanzó al menos esto (evita escribir en cada consulta).</summary>
    public const long RefrescoUltimoSegundos = 3_600;
    public const int LongitudClave = 20;

    public static bool EstaActivada(DatosLicenciaLocal datos, string hwid)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(hwid);
        return datos.Activacion is not null && string.Equals(datos.Activacion, FirmaLicencia.TokenActivacion(hwid), StringComparison.Ordinal);
    }

    /// <summary>Lee la prueba local: sin valor <c>d</c> no hay prueba; con <c>d</c> pero datos ilegibles o firma distinta, alterada.</summary>
    public static PruebaLocal LeerPrueba(DatosLicenciaLocal datos, string hwid)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(hwid);

        if (datos.Inicio is null)
        {
            return new PruebaLocal(SituacionPrueba.Ninguna, 0, 0);
        }

        if (long.TryParse(datos.Inicio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inicio)
            && datos.Ultimo is not null
            && long.TryParse(datos.Ultimo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ultimo)
            && datos.Firma is not null
            && string.Equals(datos.Firma, FirmaLicencia.Prueba(inicio, ultimo, hwid), StringComparison.Ordinal))
        {
            return new PruebaLocal(SituacionPrueba.Valida, inicio, ultimo);
        }

        return new PruebaLocal(SituacionPrueba.Alterada, 0, 0);
    }

    /// <summary>Calcula el estado a partir de los datos locales y el instante actual (epoch, segundos).</summary>
    public static CalculoLicencia Calcular(DatosLicenciaLocal datos, long ahoraEpoch, string hwid)
    {
        if (EstaActivada(datos, hwid))
        {
            return new CalculoLicencia(EstadoLicencia.Activada, false, 0, 0);
        }

        var prueba = LeerPrueba(datos, hwid);
        switch (prueba.Situacion)
        {
            case SituacionPrueba.Ninguna:
                return new CalculoLicencia(EstadoLicencia.NoIniciada, false, 0, 0);
            case SituacionPrueba.Alterada:
                return new CalculoLicencia(EstadoLicencia.VencidaPor(MotivoVencimiento.Alterada), false, 0, 0);
        }

        if (ahoraEpoch + ToleranciaRelojSegundos < prueba.Ultimo)
        {
            // El reloj del sistema fue retrocedido más de un día.
            return new CalculoLicencia(EstadoLicencia.VencidaPor(MotivoVencimiento.Reloj), false, prueba.Inicio, prueba.Ultimo);
        }

        var efectivo = Math.Max(ahoraEpoch, prueba.Ultimo);
        var reescribir = efectivo - prueba.Ultimo >= RefrescoUltimoSegundos;
        var dias = DiasPrueba - (int)((efectivo - prueba.Inicio) / SegundosDia);
        var estado = dias <= 0
            ? EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada)
            : EstadoLicencia.EnPrueba(dias);
        return new CalculoLicencia(estado, reescribir, prueba.Inicio, efectivo);
    }

    /// <summary>Quita todo carácter no alfanumérico y pasa a mayúsculas (igual que la base del servidor).</summary>
    public static string NormalizarClave(string? clave)
    {
        if (string.IsNullOrEmpty(clave))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(clave.Length);
        foreach (var c in clave)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>D9: tras normalizar deben quedar exactamente 20 caracteres hexadecimales.</summary>
    public static bool ClaveConFormatoValido(string? clave)
    {
        var normalizada = NormalizarClave(clave);
        return normalizada.Length == LongitudClave && normalizada.All(char.IsAsciiHexDigit);
    }

    /// <summary>Clave normalizada con el formato <c>XXXX-XXXX-XXXX-XXXX-XXXX</c>.</summary>
    public static string FormatearClave(string? clave)
    {
        var n = NormalizarClave(clave);
        return n.Length != LongitudClave
            ? n
            : $"{n[..4]}-{n[4..8]}-{n[8..12]}-{n[12..16]}-{n[16..20]}";
    }
}
