using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class AlmacenConfiguracionImpresionFalso : IAlmacenConfiguracionImpresion
{
    public string NombreEquipo { get; set; } = "PC-PRUEBA";
    public ConfiguracionImpresion? Configuracion { get; set; }
    public bool LanzarErrorAlGuardar { get; set; }

    public AlmacenConfiguracionImpresionFalso(ConfiguracionImpresion? inicial = null)
    {
        Configuracion = inicial;
    }

    public Task<ConfiguracionImpresion?> LeerAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Configuracion);
    }

    public Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default)
    {
        if (LanzarErrorAlGuardar)
        {
            throw new IOException("Error simulado de disco al guardar configuración.");
        }

        Configuracion = configuracion;
        return Task.CompletedTask;
    }
}
