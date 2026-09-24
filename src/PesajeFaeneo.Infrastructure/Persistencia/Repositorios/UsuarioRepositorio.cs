using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class UsuarioRepositorio : IUsuarioRepositorio
{
    private readonly PesajeDbContext _context;

    public UsuarioRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, cancellationToken);
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var query = _context.Usuarios.AsNoTracking();
        if (!incluirInactivos)
        {
            query = query.Where(u => u.Activo);
        }

        return await query.OrderBy(u => u.NombreUsuario).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario, cancellationToken);
    }

    public async Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.CountAsync(u => u.Rol == Rol.Administrador && u.Activo, cancellationToken);
    }

    public void Agregar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        _context.Usuarios.Add(usuario);
    }
}
