namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

/// <summary>
/// Transacción explícita de una unidad de trabajo. Desecharla sin ConfirmarAsync revierte.
/// </summary>
public interface ITransaccion : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancellationToken = default);
}
