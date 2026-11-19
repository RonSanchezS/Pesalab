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

    public async Task<Ganadero?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await _context.Ganaderos.FirstOrDefaultAsync(g => g.Codigo == codigo, cancellationToken);
    }

    public async Task<int> ObtenerCodigoNumericoMaximoAsync(CancellationToken cancellationToken = default)
    {
        // El catálogo es pequeño: se filtra en memoria (los códigos heredados pueden ser alfanuméricos).
        var codigos = await _context.Ganaderos.AsNoTracking().Select(g => g.Codigo).ToListAsync(cancellationToken);
        var maximo = 0;
        foreach (var codigo in codigos)
        {
            if (codigo.Length > 0 && codigo.All(char.IsAsciiDigit) && int.TryParse(codigo, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > maximo)
            {
                maximo = n;
            }
        }

        return maximo;
    }

    public async Task<IReadOnlyList<Ganadero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var query = _context.Ganaderos.AsNoTracking();
        if (!incluirInactivos)
        {
            query = query.Where(g => g.Activo);
        }

        // Longitud primero: los códigos numéricos quedan en orden numérico ("2" antes que "10").
        return await query.OrderBy(g => g.Codigo.Length).ThenBy(g => g.Codigo).ToListAsync(cancellationToken);
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
