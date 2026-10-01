using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Importacion;

public sealed class ServicioImportacionFalso : IServicioImportacion
{
    public List<string> Tablas { get; set; } = new() { "CLIENTES", "VENTAS" };
    public VistaOrigen? Vista { get; set; }
    public ResultadoImportacion? ResultadoSimulacro { get; set; }
    public ResultadoImportacion? ResultadoImportacion { get; set; }

    public Exception? ErrorListarTablas { get; set; }
    public Exception? ErrorLeer { get; set; }
    public Exception? ErrorSimular { get; set; }
    public Exception? ErrorImportar { get; set; }

    public List<string> RutasAccessConsultadas { get; } = new();
    public List<(SolicitudLectura Lectura, TipoImportacion Tipo)> LecturasSolicitadas { get; } = new();
    public List<SolicitudImportacion> SimulacionesSolicitadas { get; } = new();
    public List<(SolicitudImportacion Solicitud, string Huella)> ImportacionesSolicitadas { get; } = new();

    public Task<IReadOnlyList<string>> ListarTablasAccessAsync(string ruta, CancellationToken cancellationToken = default)
    {
        RutasAccessConsultadas.Add(ruta);
        if (ErrorListarTablas != null) throw ErrorListarTablas;
        return Task.FromResult<IReadOnlyList<string>>(Tablas);
    }

    public Task<VistaOrigen> LeerOrigenAsync(SolicitudLectura lectura, TipoImportacion tipo, CancellationToken cancellationToken = default)
    {
        LecturasSolicitadas.Add((lectura, tipo));
        if (ErrorLeer != null) throw ErrorLeer;
        return Task.FromResult(Vista ?? new VistaOrigen(
            new List<string> { "Col1", "Col2" },
            new List<FilaOrigen>(),
            10,
            "Windows-1252",
            Array.Empty<string>(),
            new Dictionary<string, int?>()));
    }

    public Task<ResultadoImportacion> SimularAsync(SolicitudImportacion solicitud, CancellationToken cancellationToken = default)
    {
        SimulacionesSolicitadas.Add(solicitud);
        if (ErrorSimular != null) throw ErrorSimular;
        return Task.FromResult(ResultadoSimulacro ?? new ResultadoImportacion(
            true, "HUELLA123", solicitud.Tipo, 10, 8, 0, 2, 0, 0, Array.Empty<Incidencia>(), false, null));
    }

    public Task<ResultadoImportacion> ImportarAsync(SolicitudImportacion solicitud, string huellaSimulacro, CancellationToken cancellationToken = default)
    {
        ImportacionesSolicitadas.Add((solicitud, huellaSimulacro));
        if (ErrorImportar != null) throw ErrorImportar;
        return Task.FromResult(ResultadoImportacion ?? new ResultadoImportacion(
            false, huellaSimulacro, solicitud.Tipo, 10, 8, 8, 2, 0, 0, Array.Empty<Incidencia>(), false, "backup-pre.zip"));
    }
}
