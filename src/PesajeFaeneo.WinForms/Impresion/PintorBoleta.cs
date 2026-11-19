using System.Drawing;
using System.Drawing.Imaging;
using PesajeFaeneo.Application.Boletas.Diseno;

namespace PesajeFaeneo.WinForms.Impresion;

internal static class PintorBoleta
{
    public const string FamiliaFuente = "Arial";

    /// <summary>
    /// Dibuja la página con origen en (origenXMm, origenYMm) del Graphics, en mm.
    /// </summary>
    public static void Pintar(Graphics g, PaginaBoleta pagina, Image? logo, float origenXMm, float origenYMm)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(pagina);

        var unidad = g.PageUnit;
        var estado = g.Save();

        try
        {
            g.PageUnit = GraphicsUnit.Millimeter;
            g.TranslateTransform(origenXMm, origenYMm);

            foreach (var elemento in pagina.Elementos)
            {
                switch (elemento)
                {
                    case TextoBoleta t:
                        PintarTexto(g, t);
                        break;
                    case LineaBoleta l:
                        PintarLinea(g, l);
                        break;
                    case RectanguloBoleta r:
                        PintarRectangulo(g, r);
                        break;
                    case LogoBoleta logoElem:
                        PintarLogo(g, logoElem, logo);
                        break;
                    case FondoLogoBoleta fondo:
                        PintarFondoLogo(g, fondo, logo);
                        break;
                }
            }
        }
        finally
        {
            g.Restore(estado);
            g.PageUnit = unidad;
        }
    }

    private static void PintarTexto(Graphics g, TextoBoleta t)
    {
        if (string.IsNullOrWhiteSpace(t.Texto))
        {
            return;
        }

        var estilo = t.Negrita ? FontStyle.Bold : FontStyle.Regular;
        using var fuente = new Font(FamiliaFuente, t.TamanoPt, estilo, GraphicsUnit.Point);

        Brush pincel;
        SolidBrush? pincelGris = null;
        if (t.Tono == TonoTinta.Gris)
        {
            pincelGris = new SolidBrush(Color.FromArgb(190, 190, 190));
            pincel = pincelGris;
        }
        else
        {
            pincel = Brushes.Black;
        }

        try
        {
            using var formato = new StringFormat
            {
                Alignment = t.Alineacion switch
                {
                    AlineacionTexto.Centro => StringAlignment.Center,
                    AlineacionTexto.Derecha => StringAlignment.Far,
                    _ => StringAlignment.Near
                },
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };

            formato.FormatFlags |= StringFormatFlags.LineLimit;
            if (!t.VariasLineas)
            {
                formato.FormatFlags |= StringFormatFlags.NoWrap;
            }

            g.DrawString(t.Texto, fuente, pincel, new RectangleF(t.X, t.Y, t.Ancho, t.Alto), formato);
        }
        finally
        {
            pincelGris?.Dispose();
        }
    }

    private static void PintarLinea(Graphics g, LineaBoleta l)
    {
        using var lapiz = new Pen(Color.Black, l.GrosorMm);
        g.DrawLine(lapiz, l.X1, l.Y1, l.X2, l.Y2);
    }

    private static void PintarRectangulo(Graphics g, RectanguloBoleta r)
    {
        using var lapiz = new Pen(Color.Black, r.GrosorMm);
        g.DrawRectangle(lapiz, r.X, r.Y, r.Ancho, r.Alto);
    }

    private static void PintarFondoLogo(Graphics g, FondoLogoBoleta fondo, Image? logo)
    {
        if (logo is null)
        {
            return;
        }

        var escala = Math.Min(fondo.Ancho / logo.Width, fondo.Alto / logo.Height);
        var ancho = logo.Width * escala;
        var alto = logo.Height * escala;
        var destino = new RectangleF(fondo.X + (fondo.Ancho - ancho) / 2f, fondo.Y + (fondo.Alto - alto) / 2f, ancho, alto);

        var matriz = new ColorMatrix { Matrix33 = Math.Clamp(fondo.Opacidad, 0f, 1f) };
        using var atributos = new ImageAttributes();
        atributos.SetColorMatrix(matriz, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

        // Con PageUnit en milímetros el destino va en mm y el origen en píxeles de la imagen.
        var puntos = new[]
        {
            new PointF(destino.Left, destino.Top),
            new PointF(destino.Right, destino.Top),
            new PointF(destino.Left, destino.Bottom)
        };
        g.DrawImage(logo, puntos, new RectangleF(0, 0, logo.Width, logo.Height), GraphicsUnit.Pixel, atributos);
    }

    private static void PintarLogo(Graphics g, LogoBoleta logoElem, Image? logo)
    {
        if (logo is null)
        {
            return;
        }

        var escala = Math.Min(logoElem.Ancho / logo.Width, logoElem.Alto / logo.Height);
        var anchoDeseado = logo.Width * escala;
        var altoDeseado = logo.Height * escala;
        var x = logoElem.X + (logoElem.Ancho - anchoDeseado) / 2f;
        var y = logoElem.Y + (logoElem.Alto - altoDeseado) / 2f;

        g.DrawImage(logo, new RectangleF(x, y, anchoDeseado, altoDeseado));
    }
}
