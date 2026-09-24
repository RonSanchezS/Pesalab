using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PesajeFaeneo.Infrastructure.Persistencia.Conversores;

/// <summary>
/// SQLite no tiene un tipo de fecha nativo con zona horaria: al leer, EF Core siempre devuelve
/// <see cref="DateTimeKind.Unspecified"/>, sin importar con qué <see cref="DateTimeKind"/> se guardó.
/// Se persiste todo en UTC (convención documentada en Agents.md §4) y, al leer, se marca explícitamente
/// el valor como <see cref="DateTimeKind.Utc"/> para que el resto de la aplicación no tenga que adivinar.
/// Los filtros "del día" (secuencia por ganadero en F4-03, reportes en F7) deben convertir los límites
/// del día local a UTC con <c>TimeProvider.LocalTimeZone</c> antes de comparar contra estas columnas.
/// Se registran vía <c>DbContext.ConfigureConventions</c> para aplicarse a toda propiedad
/// <see cref="DateTime"/>/<see cref="Nullable{DateTime}"/> del modelo sin repetir la configuración en
/// cada <c>IEntityTypeConfiguration</c>.
/// </summary>
public class DateTimeUtcConverter : ValueConverter<DateTime, DateTime>
{
    public DateTimeUtcConverter() : base(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public class DateTimeUtcNulableConverter : ValueConverter<DateTime?, DateTime?>
{
    public DateTimeUtcNulableConverter() : base(
        v => v.HasValue
            ? (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
            : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
