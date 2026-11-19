#if CAPTURAS_MANUAL
using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Infrastructure.Licencias.Demo;

// Solo build de capturas para los manuales: nunca toca la red ni el registro real. El almacén en memoria nace ya activado
// para el equipo de demostración, así el guion de capturas no ve pantallas de licencia ni modo consulta.

/// <summary>Identificador fijo (no lee el MachineGuid de la PC de desarrollo).</summary>
public sealed class IdentificadorEquipoDemo : IIdentificadorEquipo
{
    public string Hwid { get; } = FirmaLicencia.Hwid("demo-capturas-manual");
}

public sealed class AlmacenLicenciaMemoria : IAlmacenLicenciaLocal
{
    private readonly object _candado = new();
    private DatosLicenciaLocal _datos;

    public AlmacenLicenciaMemoria(IIdentificadorEquipo equipo)
    {
        _datos = DatosLicenciaLocal.Vacio with { Activacion = FirmaLicencia.TokenActivacion(equipo.Hwid) };
    }

    public DatosLicenciaLocal Leer()
    {
        lock (_candado)
        {
            return _datos;
        }
    }

    public void EscribirPrueba(long inicio, long ultimo, string firma)
    {
        lock (_candado)
        {
            _datos = _datos with
            {
                Inicio = inicio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Ultimo = ultimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Firma = firma
            };
        }
    }

    public void EscribirActivacion(string token)
    {
        lock (_candado)
        {
            _datos = _datos with { Activacion = token };
        }
    }

    public void LimpiarActivacion() => EscribirActivacion(string.Empty);
}

public sealed class ServidorLicenciasDemo : IServidorLicencias
{
    public Task<ResultadoActivacionServidor> ActivarAsync(string clave, string hwid, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoActivacionServidor.Ok);

    public Task<long?> RegistrarPruebaAsync(string hwid, CancellationToken cancellationToken = default) =>
        Task.FromResult<long?>(null);

    public Task<EstadoRemotoLicencia> ConsultarAsync(string hwid, CancellationToken cancellationToken = default) =>
        Task.FromResult(new EstadoRemotoLicencia(EstadoClaveRemota.Activada, null));
}
#endif
