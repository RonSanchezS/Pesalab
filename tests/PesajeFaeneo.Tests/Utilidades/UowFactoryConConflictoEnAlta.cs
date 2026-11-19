using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Tests.Utilidades;

/// <summary>
/// Simula dos altas simultáneas que toman el mismo código (F10, O6): las primeras <c>fallos</c> llamadas a
/// <c>GuardarCambiosAsync</c> lanzan <see cref="ConflictoException"/> sin guardar, tras ejecutar
/// <c>alFallar</c> (que suele insertar, con otro contexto, la fila "ganadora" que ocupó el código).
/// </summary>
public sealed class UowFactoryConConflictoEnAlta : IUnidadDeTrabajoFactory
{
    private readonly IUnidadDeTrabajoFactory _inner;
    private int _fallosPendientes;
    private readonly Func<Task>? _alFallar;

    public int LlamadasAGuardar { get; private set; }

    public UowFactoryConConflictoEnAlta(IUnidadDeTrabajoFactory inner, int fallos, Func<Task>? alFallar = null)
    {
        _inner = inner;
        _fallosPendientes = fallos;
        _alFallar = alFallar;
    }

    public async Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default) =>
        new Decorador(await _inner.CrearAsync(cancellationToken), this);

    private sealed class Decorador : IUnidadDeTrabajo
    {
        private readonly IUnidadDeTrabajo _inner;
        private readonly UowFactoryConConflictoEnAlta _padre;

        public Decorador(IUnidadDeTrabajo inner, UowFactoryConConflictoEnAlta padre)
        {
            _inner = inner;
            _padre = padre;
        }

        public IUsuarioRepositorio Usuarios => _inner.Usuarios;
        public IAuditoriaRepositorio Auditoria => _inner.Auditoria;
        public IGanaderoRepositorio Ganaderos => _inner.Ganaderos;
        public IClienteRepositorio Clientes => _inner.Clientes;
        public IFleteroRepositorio Fleteros => _inner.Fleteros;
        public IParametrosRepositorio Parametros => _inner.Parametros;
        public IConfiguracionBalanzaRepositorio ConfiguracionBalanza => _inner.ConfiguracionBalanza;
        public IPesajeRepositorio Pesajes => _inner.Pesajes;

        public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            _padre.LlamadasAGuardar++;
            if (_padre._fallosPendientes > 0)
            {
                _padre._fallosPendientes--;
                if (_padre._alFallar is not null)
                {
                    await _padre._alFallar();
                }

                throw new ConflictoException("Ya existe un registro con ese valor.");
            }

            await _inner.GuardarCambiosAsync(cancellationToken);
        }

        public Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default) =>
            _inner.IniciarTransaccionAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
