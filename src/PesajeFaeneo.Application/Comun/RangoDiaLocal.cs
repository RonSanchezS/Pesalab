namespace PesajeFaeneo.Application.Comun;

public readonly record struct RangoUtc(DateTime DesdeUtc, DateTime HastaUtc);

public static class RangoDiaLocal
{
    /// <summary>
    /// Día calendario local que contiene "ahora" según tiempo.LocalTimeZone.
    /// </summary>
    public static RangoUtc DeHoy(TimeProvider tiempo)
    {
        ArgumentNullException.ThrowIfNull(tiempo);

        var ahoraLocal = TimeZoneInfo.ConvertTime(tiempo.GetUtcNow(), tiempo.LocalTimeZone);
        return DelDia(DateOnly.FromDateTime(ahoraLocal.DateTime), tiempo.LocalTimeZone);
    }

    /// <summary>
    /// Día calendario "fecha" en la zona "zona".
    /// </summary>
    public static RangoUtc DelDia(DateOnly fecha, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        var inicioLocal = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var finLocal = inicioLocal.AddDays(1);

        var desdeUtc = ConvertirLocalAUtc(inicioLocal, zona);
        var hastaUtc = ConvertirLocalAUtc(finLocal, zona);

        return new RangoUtc(desdeUtc, hastaUtc);
    }

    private static DateTime ConvertirLocalAUtc(DateTime local, TimeZoneInfo zona)
    {
        var actual = local;
        var minutosAvanzados = 0;
        while (zona.IsInvalidTime(actual) && minutosAvanzados < 180)
        {
            actual = actual.AddMinutes(1);
            minutosAvanzados++;
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(actual, zona);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }
}
