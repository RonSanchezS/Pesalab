using PesajeFaeneo.Application.Utilitarios.Logs;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Logs;

public sealed class ServicioLogsFalso : IServicioLogs
{
    public string CarpetaLogs { get; set; } = @"C:\FakeLogs";
    public List<ArchivoLog> Archivos { get; set; } = new();
    public ContenidoLog? Contenido { get; set; }
    public Exception? ErrorLeer { get; set; }
    public Exception? ErrorListar { get; set; }

    public List<string> ArchivosLeidos { get; } = new();
    public int ConteoListar { get; private set; }

    public Task<IReadOnlyList<ArchivoLog>> ListarArchivosAsync(CancellationToken cancellationToken = default)
    {
        ConteoListar++;
        if (ErrorListar != null) throw ErrorListar;
        return Task.FromResult<IReadOnlyList<ArchivoLog>>(Archivos);
    }

    public Task<ContenidoLog> LeerAsync(string nombreArchivo, CancellationToken cancellationToken = default)
    {
        ArchivosLeidos.Add(nombreArchivo);
        if (ErrorLeer != null) throw ErrorLeer;
        return Task.FromResult(Contenido ?? new ContenidoLog(
            nombreArchivo,
            Array.Empty<EntradaLog>(),
            false,
            0));
    }
}
