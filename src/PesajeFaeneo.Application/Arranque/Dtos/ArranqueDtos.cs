namespace PesajeFaeneo.Application.Arranque.Dtos;

public enum EstadoActualizacionBaseDatos
{
    NuevaInstalacion = 1,
    AlDia = 2,
    Actualizada = 3
}

public enum MotivoFalloArranque
{
    VersionMasNueva = 1,
    BackupFallido = 2,
    MigracionFallida = 3
}

public sealed record ResultadoActualizacionBaseDatos(
    EstadoActualizacionBaseDatos Estado,
    IReadOnlyList<string> MigracionesAplicadas,   // vacía salvo en Actualizada
    string? ArchivoBackup);                        // solo el nombre del archivo (nunca la ruta)
