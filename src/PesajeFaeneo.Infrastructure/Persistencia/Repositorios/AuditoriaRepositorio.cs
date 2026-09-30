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
}
