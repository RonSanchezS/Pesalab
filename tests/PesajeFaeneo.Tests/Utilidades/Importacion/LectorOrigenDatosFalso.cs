using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Importacion;

public sealed class LectorOrigenDatosFalso : ILectorOrigenDatos
{
    public int ConteoLecturas { get; private set; }
    public List<SolicitudLectura> Solicitudes { get; } = new();
    public TablaOrigen? TablaAEntregar { get; set; }
    public Func<SolicitudLectura, TablaOrigen>? FabricaTabla { get; set; }
    public IReadOnlyList<string> TablasAccess { get; set; } = Array.Empty<string>();
    public Exception? ErrorALeer { get; set; }
    public Exception? ErrorAListarTablas { get; set; }

    public Task<IReadOnlyList<string>> ListarTablasAsync(string ruta, CancellationToken cancellationToken = default)
    {
        if (ErrorAListarTablas is not null)
        {
            throw ErrorAListarTablas;
        }

        return Task.FromResult(TablasAccess);
    }

    public Task<TablaOrigen> LeerAsync(SolicitudLectura solicitud, CancellationToken cancellationToken = default)
    {
        ConteoLecturas++;
        Solicitudes.Add(solicitud);

        if (ErrorALeer is not null)
        {
            throw ErrorALeer;
        }

        if (FabricaTabla is not null)
        {
            return Task.FromResult(FabricaTabla(solicitud));
        }

        if (TablaAEntregar is not null)
        {
            return Task.FromResult(TablaAEntregar);
        }

        return Task.FromResult(new TablaOrigen(
            Array.Empty<string>(),
            Array.Empty<FilaOrigen>(),
            "HUELLA_FALSA_1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890AB",
            "UTF-8",
            Array.Empty<string>()));
    }
}
