using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class ClienteRepositorio : IClienteRepositorio
{
    private readonly PesajeDbContext _context;

    public ClienteRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Cliente?> ObtenerPorCodigoAsync(int codigo, CancellationToken cancellationToken = default)
    {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Codigo == codigo, cancellationToken);
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var query = _context.Clientes.AsNoTracking();
        if (!incluirInactivos)
        {
            query = query.Where(c => c.Activo);
        }

        return await query.OrderBy(c => c.Codigo).ToListAsync(cancellationToken);
    }

    public async Task<bool> TienePesajesAsync(int clienteId, CancellationToken cancellationToken = default)
    {
        return await _context.Pesajes.AnyAsync(p => p.ClienteId == clienteId, cancellationToken);
    }

    public async Task<int?> ObtenerCodigoMaximoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Clientes.MaxAsync(c => (int?)c.Codigo, cancellationToken);
    }

    public void Agregar(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        _context.Clientes.Add(cliente);
    }
}
