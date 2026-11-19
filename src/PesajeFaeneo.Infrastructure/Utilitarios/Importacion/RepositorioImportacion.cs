using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Infrastructure.Persistencia;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

public sealed class RepositorioImportacion : IRepositorioImportacion
{
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly ILogger<RepositorioImportacion> _logger;

    public RepositorioImportacion(
        IDbContextFactory<PesajeDbContext> contextFactory,
        ILogger<RepositorioImportacion> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClavesExistentes> ObtenerClavesAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var ganaderos = await ctx.Ganaderos
            .AsNoTracking()
            .Select(g => new { g.Codigo, g.Id })
            .ToDictionaryAsync(g => g.Codigo.ToUpperInvariant(), g => g.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var clientes = await ctx.Clientes
            .AsNoTracking()
            .Select(c => new { c.Codigo, c.Id, c.Direccion })
            .ToDictionaryAsync(c => c.Codigo, c => new ClienteExistente(c.Id, c.Direccion), cancellationToken)
            .ConfigureAwait(false);

        var pesajes = await ctx.Pesajes
            .AsNoTracking()
            .Select(p => new { p.NumeroBoleta, p.OrigenPeso })
            .ToDictionaryAsync(p => p.NumeroBoleta, p => p.OrigenPeso, cancellationToken)
            .ConfigureAwait(false);

        var modo = await ctx.ParametrosSistema
            .AsNoTracking()
            .Select(p => p.ModoDesposte)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ClavesExistentes(ganaderos, clientes, pesajes, ReglasDesposte.Efectivo(modo));
    }

    public async Task<int> EscribirAsync(
        PlanImportacion plan,
        int usuarioId,
        DateTime ahoraUtc,
        AuditoriaEntidad? auditoria,
        bool confirmar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;

        await using var tx = await ctx.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var insertadas = 0;

            if (plan.Ganaderos.Count > 0)
            {
                insertadas += await InsertarEnLotesAsync(
                    ctx,
                    plan.Ganaderos.Select(g => new Ganadero
                    {
                        Codigo = g.Codigo,
                        Nombre = g.Nombre,
                        Activo = g.Activo,
                        CreadoEn = ahoraUtc
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            if (plan.Clientes.Count > 0)
            {
                insertadas += await InsertarEnLotesAsync(
                    ctx,
                    plan.Clientes.Select(c => new Cliente
                    {
                        Codigo = c.Codigo,
                        Nombre = c.Nombre,
                        Direccion = c.Direccion,
                        Activo = c.Activo,
                        CreadoEn = ahoraUtc
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            if (plan.Pesajes.Count > 0)
            {
                insertadas += await InsertarEnLotesAsync(
                    ctx,
                    plan.Pesajes.Select(p => CrearPesaje(p, usuarioId, ahoraUtc)),
                    cancellationToken).ConfigureAwait(false);

                var siguiente = plan.Pesajes.Max(p => p.NumeroBoleta) + 1;
                await ctx.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ParametrosSistema SET SiguienteNumeroBoleta = max(SiguienteNumeroBoleta, {siguiente}) WHERE Id = 1",
                    cancellationToken).ConfigureAwait(false);
            }

            if (auditoria is not null)
            {
                ctx.Auditorias.Add(auditoria);
                await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (confirmar)
            {
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            return insertadas;
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            _logger.LogError(ex, "La base de datos rechazó la importación de {Tipo}.", plan.Tipo);
            throw new ReglaNegocioException("IMP_BD", "La base de datos rechazó los datos importados; no se guardó nada. Revise el log.", ex);
        }
    }

    private static async Task<int> InsertarEnLotesAsync<T>(
        PesajeDbContext ctx,
        IEnumerable<T> entidades,
        CancellationToken ct) where T : class
    {
        var total = 0;
        var lote = new List<T>(ReglasImportacion.TamanoLote);
        foreach (var entidad in entidades)
        {
            lote.Add(entidad);
            if (lote.Count >= ReglasImportacion.TamanoLote)
            {
                await ctx.Set<T>().AddRangeAsync(lote, ct).ConfigureAwait(false);
                await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
                ctx.ChangeTracker.Clear();
                total += lote.Count;
                lote.Clear();
            }
        }

        if (lote.Count > 0)
        {
            await ctx.Set<T>().AddRangeAsync(lote, ct).ConfigureAwait(false);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            ctx.ChangeTracker.Clear();
            total += lote.Count;
            lote.Clear();
        }

        return total;
    }

    private static Pesaje CrearPesaje(PesajeAImportar p, int usuarioId, DateTime ahoraUtc)
    {
        return new Pesaje
        {
            NumeroBoleta = p.NumeroBoleta,
            FechaHora = p.FechaHoraUtc,
            GanaderoId = p.GanaderoId,
            ClienteId = p.ClienteId,
            Direccion = p.Direccion,
            Secuencia = p.Secuencia,
            ColorRaza = p.ColorRaza,
            Sexo = p.Sexo,
            TipoDesposte = p.TipoDesposte,
            PesoIzquierdo = p.PesoIzquierdo,
            PesoDerecho = p.PesoDerecho,
            PesoTotal = p.PesoTotal,
            OrigenPeso = OrigenPeso.Importado,
            Observacion = p.Observacion,
            Estado = p.Estado,
            MotivoAnulacion = p.Estado == EstadoPesaje.Anulado ? p.MotivoAnulacion : null,
            AnuladoPorId = p.Estado == EstadoPesaje.Anulado ? usuarioId : null,
            AnuladoEn = p.Estado == EstadoPesaje.Anulado ? p.FechaHoraUtc : null,
            UsuarioId = usuarioId,
            CreadoEn = ahoraUtc,
            Version = 1
        };
    }
}
