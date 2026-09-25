using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class GanaderoRepositorio : IGanaderoRepositorio
{
    private readonly PesajeDbContext _context;

    public GanaderoRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Ganadero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Ganaderos.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<Ganadero?> ObtenerPorMarcaAsync(string marca, CancellationToken cancellationToken = default)
    {
        return await _context.Ganaderos.FirstOrDefaultAsync(g => g.Marca == marca, cancellationToken);
    }

    public async Task<IReadOnlyList<Ganadero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var query = _context.Ganaderos.AsNoTracking();
        if (!incluirInactivos)
        {
            query = query.Where(g => g.Activo);
        }

        return await query.OrderBy(g => g.Marca).ToListAsync(cancellationToken);
    }

    public async Task<bool> TienePesajesAsync(int ganaderoId, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes.AnyAsync(p => p.GanaderoId == ganaderoId, cancellationToken);
    }

    public void Agregar(Ganadero ganadero)
    {
        ArgumentNullException.ThrowIfNull(ganadero);
        _context.Ganaderos.Add(ganadero);
    }
}
