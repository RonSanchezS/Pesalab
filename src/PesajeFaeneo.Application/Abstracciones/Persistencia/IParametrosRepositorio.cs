using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IParametrosRepositorio
{
    Task<ParametrosSistema> ObtenerAsync(CancellationToken cancellationToken = default);
    Task<long?> ObtenerUltimoNumeroBoletaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa atómicamente SiguienteNumeroBoleta y devuelve el número reservado (el valor ANTERIOR al incremento).
    /// Exige una transacción explícita activa en la unidad de trabajo (InvalidOperationException si no la hay).
    /// </summary>
    Task<long> ReservarNumeroBoletaAsync(CancellationToken cancellationToken = default);
}
