using System.Text.Json;
using System.Text.Json.Serialization;
using PesajeFaeneo.Application.Boletas.Dtos;
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
    public const string ConfiguracionBalanza = "ConfiguracionBalanza";
    public const string Pesaje = "Pesaje";
    public const string ConfiguracionImpresion = "ConfiguracionImpresion";
    public const string Sistema = "Sistema";

    public static readonly IReadOnlyList<string> Todas = new[]
    {
        Usuario, Ganadero, Cliente, Fletero, ParametrosSistema,
        ConfiguracionBalanza, Pesaje, ConfiguracionImpresion, Sistema
    };
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

public sealed record ConfiguracionBalanzaAuditoria(
    TipoConexion TipoConexion,
    string PuertoCom,
    int BaudRate,
    int DataBits,
    ParidadSerial Paridad,
    BitsParada BitsParada,
    string HostTcp,
    int PuertoTcp,
    string PatronRegex,
    int LecturasParaEstable,
    decimal ToleranciaKg,
    int TimeoutMs,
    DelimitadorTrama DelimitadorTrama,
    UnidadPesoTrama UnidadPeso);

public sealed record PesajeAuditoria(
    int Id,
    long NumeroBoleta,
    DateTime FechaHora,
    int GanaderoId,
    int ClienteId,
    string Direccion,
    int Secuencia,
    string ColorRaza,
    char Sexo,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    string Observacion,
    EstadoPesaje Estado,
    OrigenPeso OrigenPeso,
    int UsuarioId,
    string? MotivoAnulacion = null,
    int? AnuladoPorId = null,
    DateTime? AnuladoEn = null,
    string? MarcaGanadero = null,
    string? NombreGanadero = null,
    int? CodigoCliente = null,
    string? NombreCliente = null);

public sealed record ConfiguracionImpresionAuditoria(
    string NombreEquipo,
    string? NombreImpresora,
    FormatoBoleta Formato,
    string? NombrePapel,
    int Copias,
    bool ImprimirAlGrabar,
    decimal DesplazamientoXMm,
    decimal DesplazamientoYMm);

public sealed record ImpresionAuditoria(
    long NumeroBoleta,
    bool EsReimpresion,
    int Copias,
    string Impresora,
    FormatoBoleta Formato,
    string NombreEquipo);

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

    public static ConfiguracionBalanzaAuditoria CrearSnapshot(ConfiguracionBalanza config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ConfiguracionBalanzaAuditoria(
            config.TipoConexion,
            config.PuertoCom,
            config.BaudRate,
            config.DataBits,
            config.Paridad,
            config.BitsParada,
            config.HostTcp,
            config.PuertoTcp,
            config.PatronRegex,
            config.LecturasParaEstable,
            config.ToleranciaKg,
            config.TimeoutMs,
            config.DelimitadorTrama,
            config.UnidadPeso);
    }

    public static PesajeAuditoria CrearSnapshot(Pesaje pesaje, Ganadero? ganadero, Cliente? cliente)
    {
        ArgumentNullException.ThrowIfNull(pesaje);
        return new PesajeAuditoria(
            pesaje.Id,
            pesaje.NumeroBoleta,
            pesaje.FechaHora,
            pesaje.GanaderoId,
            pesaje.ClienteId,
            pesaje.Direccion,
            pesaje.Secuencia,
            pesaje.ColorRaza,
            pesaje.Sexo,
            pesaje.TipoDesposte,
            pesaje.PesoIzquierdo,
            pesaje.PesoDerecho,
            pesaje.PesoTotal,
            pesaje.Observacion,
            pesaje.Estado,
            pesaje.OrigenPeso,
            pesaje.UsuarioId,
            pesaje.MotivoAnulacion,
            pesaje.AnuladoPorId,
            pesaje.AnuladoEn,
            ganadero?.Marca,
            ganadero?.Nombre,
            cliente?.Codigo,
            cliente?.Nombre);
    }

    public static PesajeAuditoria CrearSnapshot(Pesaje pesaje)
    {
        ArgumentNullException.ThrowIfNull(pesaje);
        return CrearSnapshot(pesaje, pesaje.Ganadero, pesaje.Cliente);
    }

    public static ConfiguracionImpresionAuditoria CrearSnapshot(ConfiguracionImpresion c, string nombreEquipo)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(nombreEquipo);
        return new ConfiguracionImpresionAuditoria(
            nombreEquipo,
            c.NombreImpresora,
            c.Formato,
            c.NombrePapel,
            c.Copias,
            c.ImprimirAlGrabar,
            c.DesplazamientoXMm,
            c.DesplazamientoYMm);
    }

    public static ImpresionAuditoria CrearSnapshot(RegistroImpresion r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new ImpresionAuditoria(
            r.NumeroBoleta,
            r.EsReimpresion,
            r.Copias,
            r.Impresora,
            r.Formato,
            r.NombreEquipo);
    }

    private static object? NormalizarObjeto(object? obj) => obj switch
    {
        Usuario u => CrearSnapshot(u),
        Ganadero g => CrearSnapshot(g),
        Cliente c => CrearSnapshot(c),
        Fletero f => CrearSnapshot(f),
        ParametrosSistema p => CrearSnapshot(p),
        ConfiguracionBalanza cb => CrearSnapshot(cb),
        Pesaje pesaje => CrearSnapshot(pesaje),
        _ => obj
    };
}
