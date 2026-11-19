using Serilog;

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Carga el logo de la empresa para mostrarlo en pantalla (inicio y login): lo lee en flujo y lo copia a un
/// <see cref="Bitmap"/> para no dejar el archivo bloqueado en disco (como hace <c>Image.FromFile</c>). Un archivo
/// ausente o dañado no es un error: se devuelve null y el llamador muestra solo el nombre.
/// </summary>
internal static class CargadorLogoUi
{
    public static Image? Cargar(string? ruta, string contexto)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            return null;
        }

        try
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var original = Image.FromStream(fs);
            return new Bitmap(original);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        {
            Log.Warning(ex, "No se pudo cargar el logo para {Contexto}.", contexto);
            return null;
        }
    }
}
