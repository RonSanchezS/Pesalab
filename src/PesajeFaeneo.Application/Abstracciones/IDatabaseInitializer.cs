namespace PesajeFaeneo.Application.Abstracciones;

public interface IDatabaseInitializer
{
    Task InicializarAsync(CancellationToken cancellationToken = default);
}

public interface IDataSeeder
{
    Task SembrarAsync(CancellationToken cancellationToken = default);
}
