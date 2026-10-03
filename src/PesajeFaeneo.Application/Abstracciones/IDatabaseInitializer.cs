namespace PesajeFaeneo.Application.Abstracciones;

/// <summary>Migraciones de la base frente a las que conoce esta versión del sistema (F9-02).</summary>
public sealed record EstadoEsquemaBaseDatos(
    IReadOnlyList<string> Aplicadas,
    IReadOnlyList<string> Pendientes,
    IReadOnlyList<string> Desconocidas);

public interface IDatabaseInitializer
{
    Task<EstadoEsquemaBaseDatos> EvaluarAsync(CancellationToken cancellationToken = default);
    Task InicializarAsync(CancellationToken cancellationToken = default);
}

public interface IDataSeeder
{
    Task SembrarAsync(CancellationToken cancellationToken = default);
}
