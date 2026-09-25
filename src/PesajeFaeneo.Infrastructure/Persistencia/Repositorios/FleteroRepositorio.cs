using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class FleteroRepositorio : IFleteroRepositorio
{
    private readonly PesajeDbContext _context;

    public FleteroRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Fletero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Fleteros.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<Fletero?> ObtenerActivoPorOrdenAsync(int orden, CancellationToken cancellationToken = default)
    {
        return await _context.Fleteros.FirstOrDefaultAsync(f => f.Activo && f.Orden == orden, cancellationToken);
    }

    public async Task<IReadOnlyList<Fletero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var query = _context.Fleteros.AsNoTracking();
        if (!incluirInactivos)
        {
            query = query.Where(f => f.Activo);
        }

        return await query
            .OrderByDescending(f => f.Activo)
            .ThenBy(f => f.Orden)
            .ThenBy(f => f.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Fleteros.CountAsync(f => f.Activo, cancellationToken);
    }

    public void Agregar(Fletero fletero)
    {
        ArgumentNullException.ThrowIfNull(fletero);
        _context.Fleteros.Add(fletero);
    }
}
