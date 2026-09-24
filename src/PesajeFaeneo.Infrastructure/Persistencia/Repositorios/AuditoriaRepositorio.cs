using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Application.Abstracciones.Persistencia;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class AuditoriaRepositorio : IAuditoriaRepositorio
{
    private readonly PesajeDbContext _context;

    public AuditoriaRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Registrar(AuditoriaEntidad entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        _context.Auditorias.Add(entrada);
    }
}
