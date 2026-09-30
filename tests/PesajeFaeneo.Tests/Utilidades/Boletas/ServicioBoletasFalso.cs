using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class ServicioBoletasFalso : IServicioBoletas
{
    public DatosBoleta? DatosRetorno { get; set; }
    public Exception? ExcepcionObtenerPorId { get; set; }
    public Exception? ExcepcionObtenerPorNumero { get; set; }
    public Exception? ExcepcionRegistrarImpresion { get; set; }

    /// <summary>Si no es null, ObtenerPorNumeroAsync devuelve esta tarea pendiente (búsquedas lentas).</summary>
    public TaskCompletionSource<DatosBoleta>? TcsObtenerPorNumero { get; set; }

    public List<RegistroImpresion> ImpresionesRegistradas { get; } = new();
    public List<int> IdsObtenidos { get; } = new();
    public List<long> NumerosObtenidos { get; } = new();
    public int ConteoCrearMuestra { get; private set; }

    public Task<DatosBoleta> ObtenerPorIdAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        IdsObtenidos.Add(pesajeId);
        if (ExcepcionObtenerPorId is not null)
        {
            throw ExcepcionObtenerPorId;
        }

        return Task.FromResult(DatosRetorno ?? DatosBoletaDePrueba.Crear(pesajeId: pesajeId));
    }

    public Task<DatosBoleta> ObtenerPorNumeroAsync(long numeroBoleta, CancellationToken cancellationToken = default)
    {
        NumerosObtenidos.Add(numeroBoleta);
        if (ExcepcionObtenerPorNumero is not null)
        {
            throw ExcepcionObtenerPorNumero;
        }

        if (TcsObtenerPorNumero is not null)
        {
            return TcsObtenerPorNumero.Task;
        }

        return Task.FromResult(DatosRetorno ?? DatosBoletaDePrueba.Crear(numeroBoleta: numeroBoleta));
    }

    public Task RegistrarImpresionAsync(RegistroImpresion registro, CancellationToken cancellationToken = default)
    {
        if (ExcepcionRegistrarImpresion is not null)
        {
            throw ExcepcionRegistrarImpresion;
        }

        ImpresionesRegistradas.Add(registro);
        return Task.CompletedTask;
    }

    public Task<DatosBoleta> CrearMuestraAsync(CancellationToken cancellationToken = default)
    {
        ConteoCrearMuestra++;
        return Task.FromResult(DatosRetorno ?? DatosBoletaDePrueba.Crear(esMuestra: true, numeroBoleta: 0, pesajeId: 0));
    }
}
