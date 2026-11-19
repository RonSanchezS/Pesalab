using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class ServicioPesajesFalso : IServicioPesajes
{
    public List<RegistrarPesaje> DtosRegistrados { get; } = new();
    public Func<RegistrarPesaje, Task<PesajeRegistrado>>? AlRegistrarAsync { get; set; }
    public PesajeRegistrado? ResultadoRegistrar { get; set; }
    public Exception? ExcepcionAlRegistrar { get; set; }

    public int VecesSugerirLlamado { get; private set; }
    public Func<int, Task<SugerenciaSecuencia>>? AlSugerirSecuenciaAsync { get; set; }
    public SugerenciaSecuencia ResultadoSugerencia { get; set; } = new(1, 0);

    public int VecesListarDelDiaLlamado { get; private set; }
    public List<PesajeResumen> PesajesDelDia { get; } = new();

    public async Task<PesajeRegistrado> RegistrarAsync(RegistrarPesaje dto, CancellationToken cancellationToken = default)
    {
        DtosRegistrados.Add(dto);

        if (ExcepcionAlRegistrar != null)
        {
            throw ExcepcionAlRegistrar;
        }

        if (AlRegistrarAsync != null)
        {
            return await AlRegistrarAsync(dto);
        }

        return ResultadoRegistrar ?? new PesajeRegistrado(
            Id: 1,
            NumeroBoleta: 1,
            FechaHoraUtc: DateTime.UtcNow,
            PesoTotal: dto.PesoIzquierdo + dto.PesoDerecho,
            Origen: dto.CapturadoConSimulador ? OrigenPeso.Simulador : OrigenPeso.Balanza);
    }

    public async Task<SugerenciaSecuencia> SugerirSecuenciaAsync(int ganaderoId, CancellationToken cancellationToken = default)
    {
        VecesSugerirLlamado++;

        if (AlSugerirSecuenciaAsync != null)
        {
            return await AlSugerirSecuenciaAsync(ganaderoId);
        }

        return ResultadoSugerencia;
    }

    /// <summary>Días consultados con <see cref="ListarPorDiaAsync"/>, en orden.</summary>
    public List<DateOnly> DiasConsultados { get; } = new();

    /// <summary>Respuesta por día; por omisión devuelve <see cref="PesajesDelDia"/> para cualquier día.</summary>
    public Func<DateOnly, Task<IReadOnlyList<PesajeResumen>>>? AlListarPorDiaAsync { get; set; }

    public async Task<IReadOnlyList<PesajeResumen>> ListarPorDiaAsync(DateOnly dia, CancellationToken cancellationToken = default)
    {
        DiasConsultados.Add(dia);
        if (AlListarPorDiaAsync != null)
        {
            return await AlListarPorDiaAsync(dia);
        }

        return PesajesDelDia.ToList();
    }

    public Task<IReadOnlyList<PesajeResumen>> ListarDelDiaAsync(CancellationToken cancellationToken = default)
    {
        VecesListarDelDiaLlamado++;
        return Task.FromResult<IReadOnlyList<PesajeResumen>>(PesajesDelDia.ToList());
    }
}
