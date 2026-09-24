namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IUnidadDeTrabajoFactory
{
    Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default);
}
