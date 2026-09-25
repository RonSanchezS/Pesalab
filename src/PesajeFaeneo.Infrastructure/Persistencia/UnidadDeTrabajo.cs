using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly PesajeDbContext _context;
    private IUsuarioRepositorio? _usuarios;
    private IAuditoriaRepositorio? _auditoria;
    private IGanaderoRepositorio? _ganaderos;
    private IClienteRepositorio? _clientes;
    private IFleteroRepositorio? _fleteros;
    private IParametrosRepositorio? _parametros;
    private IConfiguracionBalanzaRepositorio? _configuracionBalanza;

    public UnidadDeTrabajo(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUsuarioRepositorio Usuarios => _usuarios ??= new UsuarioRepositorio(_context);
    public IAuditoriaRepositorio Auditoria => _auditoria ??= new AuditoriaRepositorio(_context);
    public IGanaderoRepositorio Ganaderos => _ganaderos ??= new GanaderoRepositorio(_context);
    public IClienteRepositorio Clientes => _clientes ??= new ClienteRepositorio(_context);
    public IFleteroRepositorio Fleteros => _fleteros ??= new FleteroRepositorio(_context);
    public IParametrosRepositorio Parametros => _parametros ??= new ParametrosRepositorio(_context);
    public IConfiguracionBalanzaRepositorio ConfiguracionBalanza => _configuracionBalanza ??= new ConfiguracionBalanzaRepositorio(_context);

    public async Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Ya hay una transacción activa en esta unidad de trabajo.");
        }

        var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new TransaccionEf(tx);
    }

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
            if (msg.Contains("FOREIGN KEY"))
            {
                throw new ReglaNegocioException("BD_FK", "El registro está relacionado con otros datos y no se puede guardar así.", ex);
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}

internal sealed class TransaccionEf : ITransaccion
{
    private readonly IDbContextTransaction _transaction;

    public TransaccionEf(IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public Task ConfirmarAsync(CancellationToken cancellationToken = default)
    {
        return _transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _transaction.DisposeAsync();
    }
}
