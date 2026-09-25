namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

/// <summary>
/// Una unidad de trabajo = un DbContext = una transacción implícita en GuardarCambiosAsync.
/// El cambio de negocio y su registro de auditoría DEBEN guardarse en la misma llamada a GuardarCambiosAsync.
/// </summary>
public interface IUnidadDeTrabajo : IAsyncDisposable
{
    IUsuarioRepositorio Usuarios { get; }
    IAuditoriaRepositorio Auditoria { get; }
    IGanaderoRepositorio Ganaderos { get; }
    IClienteRepositorio Clientes { get; }
    IFleteroRepositorio Fleteros { get; }
    IParametrosRepositorio Parametros { get; }
    IConfiguracionBalanzaRepositorio ConfiguracionBalanza { get; }
    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default);
}
