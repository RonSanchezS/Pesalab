namespace PesajeFaeneo.Application.Arranque;

using PesajeFaeneo.Application.Arranque.Dtos;

public interface IActualizadorBaseDatos
{
    /// <summary>Evalúa, copia si corresponde y migra. Lanza ArranqueBaseDatosException si la aplicación no debe abrir.</summary>
    Task<ResultadoActualizacionBaseDatos> PrepararAsync(CancellationToken cancellationToken = default);

    /// <summary>Audita una actualización (solo si Estado == Actualizada). Llamar después de sembrar.</summary>
    Task RegistrarAuditoriaAsync(ResultadoActualizacionBaseDatos resultado, CancellationToken cancellationToken = default);
}
