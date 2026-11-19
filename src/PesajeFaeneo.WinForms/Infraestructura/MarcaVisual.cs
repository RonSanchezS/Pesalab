using System.Reflection;

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Identidad visual del producto (logo «C con pesa»), embebida en el ejecutable. Es independiente del logo de la
/// empresa cliente, que se carga en Parámetros y se muestra con <see cref="CargadorLogoUi"/>: nunca lo sustituye.
/// Los recursos se generan con <c>scripts/generar-marca.py</c>.
/// </summary>
internal static class MarcaVisual
{
    private static readonly Lazy<Icon?> _icono = new(() => CargarIcono("Marca.app.ico"));

    /// <summary>Icono de las ventanas (el del ejecutable va por <c>ApplicationIcon</c>). Null si falta el recurso.</summary>
    public static Icon? Icono => _icono.Value;

    /// <summary>Marca sin texto (transparente). Cada llamada devuelve un <see cref="Bitmap"/> nuevo que debe liberar el llamador.</summary>
    public static Image? CargarIconoMarca() => CargarImagen("Marca.icono.png");

    /// <summary>Marca horizontal con nombre del producto. Cada llamada devuelve un <see cref="Bitmap"/> nuevo.</summary>
    public static Image? CargarMarcaHorizontal() => CargarImagen("Marca.horizontal.png");

    private static Icon? CargarIcono(string recurso)
    {
        using var flujo = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso);
        return flujo is null ? null : new Icon(flujo);
    }

    private static Image? CargarImagen(string recurso)
    {
        using var flujo = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso);
        if (flujo is null)
        {
            return null;
        }

        using var original = Image.FromStream(flujo);
        return new Bitmap(original);
    }
}
