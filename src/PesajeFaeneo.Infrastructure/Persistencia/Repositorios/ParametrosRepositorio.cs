using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class ParametrosRepositorio : IParametrosRepositorio
{
    private readonly PesajeDbContext _context;

    public ParametrosRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ParametrosSistema> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var parametros = await _context.ParametrosSistema.FirstOrDefaultAsync(p => p.Id == 1, cancellationToken);
        if (parametros == null)
        {
            throw new InvalidOperationException("No existe la fila de parámetros del sistema (Id = 1).");
        }

        return parametros;
    }

    public async Task<long?> ObtenerUltimoNumeroBoletaAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes.MaxAsync(p => (long?)p.NumeroBoleta, cancellationToken);
    }
}
