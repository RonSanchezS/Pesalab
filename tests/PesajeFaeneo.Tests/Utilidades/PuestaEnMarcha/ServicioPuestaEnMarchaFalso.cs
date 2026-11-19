using PesajeFaeneo.Application.PuestaEnMarcha;
using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Utilidades.PuestaEnMarcha;

/// <summary>Doble de <see cref="IServicioPuestaEnMarcha"/> con estado configurable; devuelve tareas ya completadas.</summary>
public sealed class ServicioPuestaEnMarchaFalso : IServicioPuestaEnMarcha
{
    public EstadoPuestaEnMarcha Estado { get; set; } = new(
        Completa: false,
        EmpresaConfigurada: false,
        NombreEmpresa: "NOMBRE DE EMPRESA",
        TipoConexionBalanza: TipoConexion.Simulador,
        ImpresionConfigurada: false,
        FleterosActivos: 0,
        UsuariosNoAdministradoresActivos: 0,
        ModoDesposte: null);

    public int LlamadasObtenerEstado { get; private set; }
    public List<bool> LlamadasMarcarCompleta { get; } = new();
    public Exception? ExcepcionAlMarcar { get; set; }

    public Task<EstadoPuestaEnMarcha> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        LlamadasObtenerEstado++;
        return Task.FromResult(Estado);
    }

    public Task MarcarCompletaAsync(bool confirmaSimulador, CancellationToken cancellationToken = default)
    {
        LlamadasMarcarCompleta.Add(confirmaSimulador);
        if (ExcepcionAlMarcar is not null)
        {
            return Task.FromException(ExcepcionAlMarcar);
        }

        return Task.CompletedTask;
    }
}
