using System.Text.Json;
using System.Text.Json.Serialization;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Auditoria;

public static class EntidadesAuditoria
{
    public const string Usuario = "Usuario";
}

public sealed record UsuarioAuditoria(
    int Id,
    string NombreUsuario,
    string NombreCompleto,
    Rol Rol,
    bool Activo,
    bool DebeCambiarPassword);

public static class FabricaAuditoria
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AuditoriaEntidad Crear(
        TimeProvider tiempo,
        int? usuarioId,
        AccionAuditoria accion,
        string entidad,
        string entidadId,
        object? antes = null,
        object? despues = null,
        string? motivo = null)
    {
        ArgumentNullException.ThrowIfNull(tiempo);
        ArgumentNullException.ThrowIfNull(entidad);
        ArgumentNullException.ThrowIfNull(entidadId);

        var idTruncado = entidadId.Length > 50 ? entidadId[..50] : entidadId;
        var motivoTruncado = motivo is not null && motivo.Length > 250 ? motivo[..250] : motivo;

        var objAntes = NormalizarObjeto(antes);
        var objDespues = NormalizarObjeto(despues);

        string? antesJson = objAntes is null ? null : (objAntes is string sAntes ? sAntes : JsonSerializer.Serialize(objAntes, JsonOptions));
        string? despuesJson = objDespues is null ? null : (objDespues is string sDespues ? sDespues : JsonSerializer.Serialize(objDespues, JsonOptions));

        return new Domain.Entities.Auditoria
        {
            FechaHora = tiempo.GetUtcNow().UtcDateTime,
            UsuarioId = usuarioId,
            Accion = accion,
            Entidad = entidad,
            EntidadId = idTruncado,
            DatosAntesJson = antesJson,
            DatosDespuesJson = despuesJson,
            Motivo = motivoTruncado
        };
    }

    public static UsuarioAuditoria CrearSnapshot(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return new UsuarioAuditoria(
            usuario.Id,
            usuario.NombreUsuario,
            usuario.NombreCompleto,
            usuario.Rol,
            usuario.Activo,
            usuario.DebeCambiarPassword);
    }

    private static object? NormalizarObjeto(object? obj) => obj switch
    {
        Usuario u => CrearSnapshot(u),
        _ => obj
    };
}
