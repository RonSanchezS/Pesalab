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

    public async Task<Pesaje?> ObtenerParaEdicionAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Pesaje>> BuscarAsync(CriteriosBusquedaPesaje criterios, int maximo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        IQueryable<Pesaje> q = _context.Pesajes
            .AsNoTracking()
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente);

        if (criterios.DesdeUtc is { } d)
        {
            var dUtc = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            q = q.Where(p => p.FechaHora >= dUtc);
        }

        if (criterios.HastaUtc is { } h)
        {
            var hUtc = DateTime.SpecifyKind(h, DateTimeKind.Utc);
            q = q.Where(p => p.FechaHora < hUtc);
        }

        if (criterios.NumeroBoleta is { } num)
        {
            q = q.Where(p => p.NumeroBoleta == num);
        }

        if (criterios.GanaderoId is { } gid)
        {
            q = q.Where(p => p.GanaderoId == gid);
        }

        if (criterios.ClienteId is { } cid)
        {
            q = q.Where(p => p.ClienteId == cid);
        }

        if (criterios.Estado is { } est)
        {
            q = q.Where(p => p.Estado == est);
        }

        return await q.OrderByDescending(p => p.NumeroBoleta)
            .Take(maximo)
            .ToListAsync(cancellationToken);
    }

    // Expuesto (internal) para las pruebas del plan de consulta (HU-F7-02): deben verificar el SQL EXACTO que ejecuta
    // ListarParaReporteAsync, no una copia escrita a mano.
    internal IQueryable<Pesaje> ConsultaReporte(CriteriosReporte criterios, int maximo)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        var dUtc = DateTime.SpecifyKind(criterios.DesdeUtc, DateTimeKind.Utc);
        var hUtc = DateTime.SpecifyKind(criterios.HastaUtc, DateTimeKind.Utc);
        var estado = criterios.Estado;

        IQueryable<Pesaje> q = _context.Pesajes
            .AsNoTracking()
            .Where(p => p.Estado == estado && p.FechaHora >= dUtc && p.FechaHora < hUtc);

        if (criterios.GanaderoId is { } gid)
        {
            q = q.Where(p => p.GanaderoId == gid);
        }

        if (criterios.ClienteId is { } cid)
        {
            q = q.Where(p => p.ClienteId == cid);
        }

        return q
            .Include(p => p.Ganadero)
            .Include(p => p.Cliente)
            .Include(p => p.AnuladoPor)
            .OrderBy(p => p.FechaHora)
            .ThenBy(p => p.NumeroBoleta)
            .Take(maximo);
    }

    public async Task<IReadOnlyList<Pesaje>> ListarParaReporteAsync(CriteriosReporte criterios, int maximo, CancellationToken cancellationToken = default)
    {
        if (maximo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximo));
        }

        return await ConsultaReporte(criterios, maximo).ToListAsync(cancellationToken);
    }
}
