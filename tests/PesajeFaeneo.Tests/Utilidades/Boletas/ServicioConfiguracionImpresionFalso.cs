using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class ServicioConfiguracionImpresionFalso : IServicioConfiguracionImpresion
{
    public ConfiguracionImpresionEquipo EquipoRetorno { get; set; } =
        new(ReglasImpresion.PorDefecto, Configurada: true, NombreEquipo: "PC-TEST");

    public Exception? ExcepcionGuardar { get; set; }
    public List<ConfiguracionImpresion> ConfiguracionesGuardadas { get; } = new();

    public Task<ConfiguracionImpresionEquipo> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EquipoRetorno);
    }

    public Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default)
    {
        if (ExcepcionGuardar is not null)
        {
            throw ExcepcionGuardar;
        }

        ConfiguracionesGuardadas.Add(configuracion);
        EquipoRetorno = new ConfiguracionImpresionEquipo(configuracion, Configurada: true, NombreEquipo: EquipoRetorno.NombreEquipo);
        return Task.CompletedTask;
    }
}
