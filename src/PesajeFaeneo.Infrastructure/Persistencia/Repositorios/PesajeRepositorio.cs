using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class PesajeRepositorio : IPesajeRepositorio
{
    private readonly PesajeDbContext _context;

    public PesajeRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Agregar(Pesaje pesaje)
    {
        ArgumentNullException.ThrowIfNull(pesaje);
        _context.Pesajes.Add(pesaje);
    }

    public async Task<Pesaje?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes
            .AsNoTracking()
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<int?> ObtenerSecuenciaMaximaAsync(
        int ganaderoId,
        DateTime desdeUtc,
        DateTime hastaUtc,
        CancellationToken cancellationToken = default)
    {
        var dUtc = DateTime.SpecifyKind(desdeUtc, DateTimeKind.Utc);
        var hUtc = DateTime.SpecifyKind(hastaUtc, DateTimeKind.Utc);

        return await _context.Pesajes
            .Where(p => p.GanaderoId == ganaderoId
                        && p.Estado == EstadoPesaje.Vigente
                        && p.FechaHora >= dUtc
                        && p.FechaHora < hUtc)
            .MaxAsync(p => (int?)p.Secuencia, cancellationToken);
    }

    public async Task<int> ContarVigentesAsync(
        int ganaderoId,
        DateTime desdeUtc,
        DateTime hastaUtc,
        CancellationToken cancellationToken = default)
    {
        var dUtc = DateTime.SpecifyKind(desdeUtc, DateTimeKind.Utc);
        var hUtc = DateTime.SpecifyKind(hastaUtc, DateTimeKind.Utc);

        return await _context.Pesajes
            .CountAsync(p => p.GanaderoId == ganaderoId
                             && p.Estado == EstadoPesaje.Vigente
                             && p.FechaHora >= dUtc
                             && p.FechaHora < hUtc, cancellationToken);
    }

    public async Task<IReadOnlyList<Pesaje>> ListarPorRangoAsync(
        DateTime desdeUtc,
        DateTime hastaUtc,
        int maximo,
        CancellationToken cancellationToken = default)
    {
        var dUtc = DateTime.SpecifyKind(desdeUtc, DateTimeKind.Utc);
        var hUtc = DateTime.SpecifyKind(hastaUtc, DateTimeKind.Utc);

        return await _context.Pesajes
            .AsNoTracking()
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .Where(p => p.FechaHora >= dUtc && p.FechaHora < hUtc)
            .OrderByDescending(p => p.NumeroBoleta)
            .Take(maximo)
            .ToListAsync(cancellationToken);
    }

    public async Task<Pesaje?> ObtenerParaBoletaPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes
            .AsNoTracking()
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Pesaje?> ObtenerParaBoletaPorNumeroAsync(long numeroBoleta, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes
            .AsNoTracking()
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.NumeroBoleta == numeroBoleta, cancellationToken);
    }
}
