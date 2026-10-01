using System.Globalization;
using Microsoft.EntityFrameworkCore;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class AuditoriaRepositorio : IAuditoriaRepositorio
{
    private readonly PesajeDbContext _context;

    public AuditoriaRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Registrar(AuditoriaEntidad entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        _context.Auditorias.Add(entrada);
    }

    public async Task<int> ContarImpresionesPesajeAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        var entidadId = pesajeId.ToString(CultureInfo.InvariantCulture);
        return await _context.Auditorias.CountAsync(a =>
            a.Entidad == EntidadesAuditoria.Pesaje &&
            a.EntidadId == entidadId &&
            (a.Accion == AccionAuditoria.Imprimir || a.Accion == AccionAuditoria.Reimprimir),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AuditoriaEntidad>> ConsultarAsync(CriteriosAuditoria criterios, int maximo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        IQueryable<AuditoriaEntidad> q = _context.Auditorias
            .AsNoTracking()
            .Include(a => a.Usuario);

        if (criterios.DesdeUtc is { } d)
        {
            var dUtc = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            q = q.Where(a => a.FechaHora >= dUtc);
        }

        if (criterios.HastaUtc is { } h)
        {
            var hUtc = DateTime.SpecifyKind(h, DateTimeKind.Utc);
            q = q.Where(a => a.FechaHora < hUtc);
        }

        if (!string.IsNullOrWhiteSpace(criterios.Entidad))
        {
            var entidad = criterios.Entidad;
            q = q.Where(a => a.Entidad == entidad);
        }

        if (criterios.Accion is { } acc)
        {
            q = q.Where(a => a.Accion == acc);
        }

        if (criterios.UsuarioId is { } uid)
        {
            q = q.Where(a => a.UsuarioId == uid);
        }

        if (!string.IsNullOrWhiteSpace(criterios.EntidadId))
        {
            var entidadId = criterios.EntidadId;
            q = q.Where(a => a.EntidadId == entidadId);
        }

        return await q.OrderByDescending(a => a.FechaHora)
            .ThenByDescending(a => a.Id)
            .Take(maximo)
            .ToListAsync(cancellationToken);
    }
}
