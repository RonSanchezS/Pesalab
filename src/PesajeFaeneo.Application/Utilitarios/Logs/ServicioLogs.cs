using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Utilitarios.Logs;

public sealed class ServicioLogs : IServicioLogs
{
    internal const long MaximoBytes = 4 * 1024 * 1024;
    internal const int MaximoEntradas = 10_000;

    private readonly ILectorArchivosLog _lector;
    private readonly ISesionActual _sesion;

    public ServicioLogs(ILectorArchivosLog lector, ISesionActual sesion)
    {
        _lector = lector ?? throw new ArgumentNullException(nameof(lector));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
    }

    public string CarpetaLogs => _lector.Carpeta;

    public Task<IReadOnlyList<ArchivoLog>> ListarArchivosAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.GestionarParametros);
        return Task.Run(() => _lector.Listar(), cancellationToken);
    }

    public Task<ContenidoLog> LeerAsync(string nombreArchivo, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.GestionarParametros);
        return Task.Run(() =>
        {
            var (texto, truncadoBytes, tamanoBytes) = _lector.LeerFinal(nombreArchivo, MaximoBytes);
            var entradas = ParserLog.Analizar(texto);
            var invertidas = entradas.Reverse().ToList();
            bool truncado = truncadoBytes;

            if (invertidas.Count > MaximoEntradas)
            {
                invertidas = invertidas.Take(MaximoEntradas).ToList();
                truncado = true;
            }

            return new ContenidoLog(nombreArchivo, invertidas, truncado, tamanoBytes);
        }, cancellationToken);
    }
}
