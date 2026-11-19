using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Tests.Utilidades.Licencias;

/// <summary>
/// Doble de <see cref="IServicioLicencia"/> para las pruebas de otros servicios y formularios. Por defecto está activado
/// (permite operar); <see cref="Estado"/> se cambia a mano y <see cref="CambiarEstado"/> dispara el evento.
/// </summary>
public sealed class ServicioLicenciaFalso : IServicioLicencia
{
    public EstadoLicencia Estado { get; set; } = EstadoLicencia.Activada;

    public string CodigoEquipo { get; set; } = "AB12-CD34";

    public int VecesExigido { get; private set; }

    public Func<string, EstadoLicencia>? AlActivar { get; set; }

    public Func<EstadoLicencia>? AlIniciarPrueba { get; set; }

    public List<string> ClavesRecibidas { get; } = new();

    public int VecesPruebaIniciada { get; private set; }

    public event EventHandler<EstadoLicencia>? EstadoCambiado;

    public bool PermiteOperar => Estado.PermiteOperar;

    public EstadoLicencia ObtenerEstado() => Estado;

    public void ExigirOperacion()
    {
        VecesExigido++;
        if (!Estado.PermiteOperar)
        {
            throw new ReglaNegocioException(ServicioLicencia.CodigoModoConsulta, "modo consulta");
        }
    }

    public Task<EstadoLicencia> ActivarAsync(string clave, CancellationToken cancellationToken = default)
    {
        ClavesRecibidas.Add(clave);
        if (AlActivar is null)
        {
            throw new InvalidOperationException("Sin comportamiento configurado.");
        }

        var estado = AlActivar(clave);
        CambiarEstado(estado);
        return Task.FromResult(estado);
    }

    public Task<EstadoLicencia> IniciarPruebaAsync(CancellationToken cancellationToken = default)
    {
        VecesPruebaIniciada++;
        if (AlIniciarPrueba is null)
        {
            throw new InvalidOperationException("Sin comportamiento configurado.");
        }

        var estado = AlIniciarPrueba();
        CambiarEstado(estado);
        return Task.FromResult(estado);
    }

    public Task<EstadoLicencia> SincronizarAsync(CancellationToken cancellationToken = default) => Task.FromResult(Estado);

    public void CambiarEstado(EstadoLicencia estado)
    {
        Estado = estado;
        EstadoCambiado?.Invoke(this, estado);
    }
}
