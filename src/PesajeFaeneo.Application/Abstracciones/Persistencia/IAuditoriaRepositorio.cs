using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IAuditoriaRepositorio
{
    void Registrar(AuditoriaEntidad entrada);
}
