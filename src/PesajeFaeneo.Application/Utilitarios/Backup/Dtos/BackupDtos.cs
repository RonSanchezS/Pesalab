namespace PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public enum TipoBackup
{
    Manual = 1,
    Automatico = 2,
    PreRestauracion = 3,
    PreImportacion = 4,
    PreMigracion = 5
}

/// <summary>Una copia de seguridad en disco. RutaArchivo es para la UI; nunca va al log (regla 0.14).</summary>
public sealed record InfoBackup(
    string RutaArchivo,
    string NombreArchivo,
    TipoBackup Tipo,
    DateTime CreadoEnUtc,
    long TamanoBytes,
    string VersionAplicacion,
    string Equipo,
    string UltimaMigracion,
    int Pesajes,
    long? UltimoNumeroBoleta,
    bool Valido,
    string? Problema);

public sealed record SolicitudBackup(TipoBackup Tipo, int DiasRetencion, string? CarpetaSecundaria);

public sealed record ResultadoCreacionBackup(InfoBackup Backup, string? AdvertenciaCopiaSecundaria);

public sealed record ValidacionRestauracion(
    string Token,
    InfoBackup Backup,
    int Ganaderos,
    int Clientes,
    int UsuariosActivos,
    int AdministradoresActivos,
    bool RequiereActualizarFormato,
    IReadOnlyList<string> Advertencias);

public sealed record ConfiguracionBackup(bool AutomaticoActivo, bool AlCerrar, int DiasRetencion, string? CarpetaSecundaria)
{
    public static ConfiguracionBackup PorDefecto { get; } = new(true, true, ReglasBackup.DiasRetencionPorDefecto, null);
}

public sealed record ResultadoBackupAutomatico(DateTime EnUtc, bool Exito, string? NombreArchivo, string? Mensaje);

public enum EstadoRestauracionArranque
{
    NoHabia = 0,
    Aplicada = 1,
    Fallida = 2
}

public sealed record ResultadoRestauracionArranque(
    EstadoRestauracionArranque Estado,
    string? Mensaje,
    string? ArchivoOrigen,
    DateTime? FechaBackupUtc,
    string? SolicitadoPor,
    string? CarpetaReemplazada)
{
    public static ResultadoRestauracionArranque NoHabia { get; } = new(EstadoRestauracionArranque.NoHabia, null, null, null, null, null);
}
