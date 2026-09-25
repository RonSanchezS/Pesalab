namespace PesajeFaeneo.Application.Abstracciones;

public interface IAlmacenLogo
{
    Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default);
    string? ObtenerRutaCompleta(string? nombreArchivo);
    void Eliminar(string? nombreArchivo);
}
