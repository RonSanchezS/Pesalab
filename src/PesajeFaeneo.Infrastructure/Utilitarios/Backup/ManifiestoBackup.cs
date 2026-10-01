namespace PesajeFaeneo.Infrastructure.Utilitarios.Backup;

using System.Text.Json;
using System.Text.Json.Serialization;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

internal sealed record ManifiestoBackup(
    int Formato,
    string Aplicacion,
    string VersionAplicacion,
    string Equipo,
    TipoBackup Tipo,
    DateTime CreadoEnUtc,
    string UltimaMigracion,
    IReadOnlyList<string> Migraciones,
    string Sha256BaseDatos,
    long TamanoBaseDatos,
    int Pesajes,
    int Ganaderos,
    int Clientes,
    int Usuarios,
    long? UltimoNumeroBoleta,
    IReadOnlyList<string> Logos);

internal sealed record MarcadorRestauracion(
    int Version,
    string Carpeta,
    string Sha256BaseDatos,
    string ArchivoOrigen,
    DateTime FechaBackupUtc,
    string SolicitadoPor,
    DateTime SolicitadoEnUtc);

internal static class JsonBackup
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
