using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PesajeFaeneo.WinForms.Impresion;

internal static class CargadorLogo
{
    /// <summary>
    /// Carga una imagen de logo. Devuelve null si la ruta es null, no existe o la imagen es ilegible.
    /// Nunca deja el archivo bloqueado.
    /// </summary>
    public static Image? Cargar(string? ruta, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            return null;
        }

        try
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var original = Image.FromStream(fs);
            return new Bitmap(original);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException)
        {
            logger.LogWarning(ex, "No se pudo cargar el logo de la boleta desde {Ruta}. Se continuará la impresión sin logo.", ruta);
            return null;
        }
    }
}
