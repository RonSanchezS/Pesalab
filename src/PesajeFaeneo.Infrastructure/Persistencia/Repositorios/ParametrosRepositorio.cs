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

    public async Task<long> ReservarNumeroBoletaAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("ReservarNumeroBoletaAsync requiere una transacción explícita.");

        var numeros = await _context.Database
            .SqlQuery<long>($"UPDATE ParametrosSistema SET SiguienteNumeroBoleta = SiguienteNumeroBoleta + 1 WHERE Id = 1 RETURNING SiguienteNumeroBoleta - 1 AS Value")
            .ToListAsync(cancellationToken);

        return numeros.Count == 1
            ? numeros[0]
            : throw new InvalidOperationException("No existe la fila de parámetros del sistema (Id = 1).");
    }
}
