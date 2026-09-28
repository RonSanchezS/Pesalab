using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Comun;
using Xunit;

namespace PesajeFaeneo.Tests;

public class RangoDiaLocalTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    [Fact]
    public void DelDia_ZonaBolivia_CalculaRangoUtcCorrecto()
    {
        var fecha = new DateOnly(2026, 9, 26);
        var rango = RangoDiaLocal.DelDia(fecha, ZonaBolivia);

        var desdeEsperado = new DateTime(2026, 9, 26, 4, 0, 0, DateTimeKind.Utc);
        var hastaEsperado = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(desdeEsperado, rango.DesdeUtc);
        Assert.Equal(hastaEsperado, rango.HastaUtc);
        Assert.Equal(DateTimeKind.Utc, rango.DesdeUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, rango.HastaUtc.Kind);
    }

    [Fact]
    public void DeHoy_BoliviaEnMadrugadaUtc_IdentificaDiaLocalAnterior()
    {
        // 2026-09-27 02:30:00Z en UTC -> en Bolivia (-04:00) son las 22:30:00 del 2026-09-26.
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 27, 2, 30, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(ZonaBolivia);

        var rango = RangoDiaLocal.DeHoy(tiempo);

        var desdeEsperado = new DateTime(2026, 9, 26, 4, 0, 0, DateTimeKind.Utc);
        var hastaEsperado = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(desdeEsperado, rango.DesdeUtc);
        Assert.Equal(hastaEsperado, rango.HastaUtc);
    }

    [Fact]
    public void DelDia_ZonaConTransicionEnMedianoche_AvanzaInstanteInvalidoSinLanzar()
    {
        // Regla de ajuste donde a medianoche el reloj salta 1 hora hacia adelante (de 00:00 a 01:00)
        var transicionInicio = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 0, 0, 0), 10, 15);
        var transicionFin = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 1, 0, 0), 12, 15);

        var regla = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            transicionInicio,
            transicionFin);

        var zonaConSalto = TimeZoneInfo.CreateCustomTimeZone(
            "ZonaSaltoMedianoche_Test",
            TimeSpan.Zero,
            "Zona con salto",
            "Estándar",
            "Verano",
            new[] { regla });

        var fechaConSalto = new DateOnly(2026, 10, 15);
        var rango = RangoDiaLocal.DelDia(fechaConSalto, zonaConSalto);

        // No lanza excepción y devuelve rangos UTC válidos
        Assert.Equal(DateTimeKind.Utc, rango.DesdeUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, rango.HastaUtc.Kind);
        Assert.True(rango.HastaUtc > rango.DesdeUtc);
    }
}
