using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public sealed record CriteriosBusquedaPesaje(
    DateTime? DesdeUtc,               // inclusivo
    DateTime? HastaUtc,               // EXCLUSIVO
    long? NumeroBoleta,
    int? GanaderoId,
    int? ClienteId,
    EstadoPesaje? Estado);

public sealed record CriteriosAuditoria(
    DateTime? DesdeUtc,               // inclusivo
    DateTime? HastaUtc,               // EXCLUSIVO
    string? Entidad,
    AccionAuditoria? Accion,
    int? UsuarioId,
    string? EntidadId);
