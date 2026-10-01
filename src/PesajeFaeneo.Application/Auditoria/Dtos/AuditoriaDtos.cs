using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Auditoria.Dtos;

public sealed record FiltroAuditoria(
    DateOnly? Desde,
    DateOnly? Hasta,
    string? Entidad,
    AccionAuditoria? Accion,
    int? UsuarioId,
    long? NumeroBoleta);

public sealed record RegistroAuditoria(
    long Id,
    DateTime FechaHoraLocal,          // Unspecified, hora local
    string Usuario,                   // NombreUsuario; "(sistema)" si UsuarioId es null
    AccionAuditoria Accion,
    string Entidad,
    string EntidadId,                 // enmascarado en LoginFallido sin usuario (D19)
    string? Motivo,
    string? DatosAntesJson,
    string? DatosDespuesJson);

public sealed record ResultadoAuditoria(
    IReadOnlyList<RegistroAuditoria> Registros,
    bool Truncado);

public sealed record UsuarioFiltro(int Id, string NombreUsuario, string NombreCompleto);
