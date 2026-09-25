using System.Text.Json;
using System.Text.Json.Serialization;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Auditoria;

public static class EntidadesAuditoria
{
    public const string Usuario = "Usuario";
    public const string Ganadero = "Ganadero";
    public const string Cliente = "Cliente";
    public const string Fletero = "Fletero";
    public const string ParametrosSistema = "ParametrosSistema";
}

public sealed record UsuarioAuditoria(
    int Id,
    string NombreUsuario,
    string NombreCompleto,
    Rol Rol,
    bool Activo,
    bool DebeCambiarPassword);

public sealed record GanaderoAuditoria(int Id, string Marca, string Nombre, bool Activo);
public sealed record ClienteAuditoria(int Id, int Codigo, string Nombre, string Direccion, bool Activo);
public sealed record FleteroAuditoria(int Id, int Orden, string Nombre, string Celular, bool Activo);
public sealed record ParametrosAuditoria(
    string NombreEmpresa,
    string Direccion,
    string Telefono,
    string Ciudad,
    string RegistroSanitario,
    string TelefonosReclamos,
    string ObservacionPorDefecto,
    string? LogoPath,
    long SiguienteNumeroBoleta);

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

    public static GanaderoAuditoria CrearSnapshot(Ganadero ganadero)
    {
        ArgumentNullException.ThrowIfNull(ganadero);
        return new GanaderoAuditoria(
            ganadero.Id,
            ganadero.Marca,
            ganadero.Nombre,
            ganadero.Activo);
    }

    public static ClienteAuditoria CrearSnapshot(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        return new ClienteAuditoria(
            cliente.Id,
            cliente.Codigo,
            cliente.Nombre,
            cliente.Direccion,
            cliente.Activo);
    }

    public static FleteroAuditoria CrearSnapshot(Fletero fletero)
    {
        ArgumentNullException.ThrowIfNull(fletero);
        return new FleteroAuditoria(
            fletero.Id,
            fletero.Orden,
            fletero.Nombre,
            fletero.Celular,
            fletero.Activo);
    }

    public static ParametrosAuditoria CrearSnapshot(ParametrosSistema parametros)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        return new ParametrosAuditoria(
            parametros.NombreEmpresa,
            parametros.Direccion,
            parametros.Telefono,
            parametros.Ciudad,
            parametros.RegistroSanitario,
            parametros.TelefonosReclamos,
            parametros.ObservacionPorDefecto,
            parametros.LogoPath,
            parametros.SiguienteNumeroBoleta);
    }

    private static object? NormalizarObjeto(object? obj) => obj switch
    {
        Usuario u => CrearSnapshot(u),
        Ganadero g => CrearSnapshot(g),
        Cliente c => CrearSnapshot(c),
        Fletero f => CrearSnapshot(f),
        ParametrosSistema p => CrearSnapshot(p),
        _ => obj
    };
}
