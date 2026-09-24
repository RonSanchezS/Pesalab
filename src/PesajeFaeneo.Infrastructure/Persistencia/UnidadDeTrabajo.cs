using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly PesajeDbContext _context;
    private IUsuarioRepositorio? _usuarios;
    private IAuditoriaRepositorio? _auditoria;

    public UnidadDeTrabajo(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUsuarioRepositorio Usuarios => _usuarios ??= new UsuarioRepositorio(_context);
    public IAuditoriaRepositorio Auditoria => _auditoria ??= new AuditoriaRepositorio(_context);

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrenciaException("El registro fue modificado por otro usuario.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            var msg = sqliteEx.Message.ToUpperInvariant();
            if (msg.Contains("UNIQUE"))
            {
                throw new ConflictoException("Ya existe un registro con ese valor.", ex);
            }
            if (msg.Contains("CHECK"))
            {
                throw new ReglaNegocioException("BD_CHECK", "Los datos no cumplen una regla de integridad.", ex);
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
