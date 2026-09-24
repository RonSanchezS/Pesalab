using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PesajeFaeneo.Infrastructure.Persistencia.Conversores;

/// <summary>
/// Convierte <see cref="decimal"/> a <see cref="double"/> (columna SQLite <c>REAL</c>), redondeando a
/// 1 decimal (resolución de peso: 0,1 kg) al leer. Centralizado aquí porque antes estaba duplicado en
/// <c>PesajeConfiguration</c> y <c>ConfiguracionBalanzaConfiguration</c>.
/// </summary>
public static class DecimalARealConverter
{
    public static readonly ValueConverter<decimal, double> Instancia = new(
        v => (double)v,
        v => Math.Round((decimal)v, 1, MidpointRounding.AwayFromZero));
}
