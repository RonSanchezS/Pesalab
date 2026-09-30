using PesajeFaeneo.Application.Abstracciones;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class AlmacenLogoFalso : IAlmacenLogo
{
    public string? RutaConfigurada { get; set; }

    public AlmacenLogoFalso(string? rutaConfigurada = null)
    {
        RutaConfigurada = rutaConfigurada;
    }

    public string? ObtenerRutaCompleta(string? nombreArchivo) => RutaConfigurada;

    public Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RutaConfigurada ?? "logo.png");
    }

    public void Eliminar(string? nombreArchivo)
    {
    }
}
