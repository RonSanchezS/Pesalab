using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace PesajeFaeneo.Tests.Utilidades;

/// <summary>
/// Detecta defectos de presentación que solo se ven en pantalla: controles que se salen de su contenedor
/// (quedan recortados) y etiquetas o botones cuyo texto no entra en su tamaño.
/// </summary>
/// <remarks>
/// El host de pruebas corre a 96 DPI. <see cref="VerificarEscalado"/> aplica además un factor 1,25 con
/// <see cref="Control.Scale(SizeF)"/>, el mismo camino que usa AutoScaleMode.Dpi con Windows al 125 %, para
/// detectar contenedores que no acompañan el escalado (p. ej. una columna de TableLayoutPanel sin estilo).
/// </remarks>
public static class VerificadorLayout
{
    public static void Verificar(Form form)
    {
        System.Windows.Forms.Application.DoEvents();
        var problemas = Problemas(form, medirTextos: true);
        Assert.True(problemas.Count == 0, "Defectos de layout:" + Environment.NewLine + string.Join(Environment.NewLine, problemas));
    }

    public static void VerificarEscalado(Form form, float factor = 1.25f)
    {
        form.Scale(new SizeF(factor, factor));
        System.Windows.Forms.Application.DoEvents();
        var problemas = Problemas(form, medirTextos: false);
        Assert.True(problemas.Count == 0, $"Defectos de layout con escalado {factor}:" + Environment.NewLine + string.Join(Environment.NewLine, problemas));
    }

    public static List<string> Problemas(Form form, bool medirTextos)
    {
        var problemas = new List<string>();
        foreach (var c in Descendientes(form).Where(EsVisible))
        {
            var nombre = $"{c.GetType().Name} '{Resumir(c.Text)}'";

            if (c.Parent is { } p and not Form && p is not ScrollableControl { AutoScroll: true })
            {
                var r = p.ClientRectangle;
                if (c.Right > r.Right + 1 || c.Bottom > r.Bottom + 1)
                {
                    problemas.Add($"{nombre} {c.Bounds} se sale de {p.GetType().Name} '{Resumir(p.Text)}' {r}");
                }
            }

            if (c is DataGridView { AutoSizeColumnsMode: DataGridViewAutoSizeColumnsMode.Fill } g)
            {
                var suma = g.Columns.Cast<DataGridViewColumn>().Where(col => col.Visible).Sum(col => col.Width);
                if (suma > g.ClientSize.Width + 1)
                {
                    problemas.Add($"{nombre}: columnas ({suma} px) más anchas que la grilla ({g.ClientSize.Width} px)");
                }
            }

            if (medirTextos && !string.IsNullOrWhiteSpace(c.Text) && TextoCortado(c) is { } motivo)
            {
                problemas.Add($"{nombre}: {motivo}");
            }
        }

        return problemas;
    }

    private static string? TextoCortado(Control c)
    {
        switch (c)
        {
            case Label { AutoSize: false } l:
            {
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var disponible = new Size(Math.Max(l.ClientSize.Width - l.Padding.Horizontal, 1), int.MaxValue);
                var necesario = TextRenderer.MeasureText(l.Text, l.Font, disponible, flags);
                var alto = l.ClientSize.Height - l.Padding.Vertical;
                var ancho = TextRenderer.MeasureText(PalabraMasLarga(l.Text), l.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                if (necesario.Height > alto + 1 || ancho > disponible.Width + 1)
                {
                    return $"texto de {necesario} no entra en {l.ClientSize}";
                }

                return null;
            }
            case Button b:
            {
                var necesario = TextRenderer.MeasureText(b.Text, b.Font);
                // Borde, foco y relleno del botón: ~8 px por lado a 96 DPI.
                if (necesario.Width + 8 > b.ClientSize.Width || necesario.Height + 4 > b.ClientSize.Height)
                {
                    return $"texto de {necesario} no entra en el botón {b.ClientSize}";
                }

                return null;
            }
            default:
                return null;
        }
    }

    private static string PalabraMasLarga(string texto) =>
        texto.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;

    // Visible (no solo la propiedad): el control y todos sus contenedores deben estar visibles.
    private static bool EsVisible(Control c) => c.Visible && c.Width > 0 && c.Height > 0;

    private static string Resumir(string? texto) =>
        texto is null ? string.Empty : texto.Length <= 40 ? texto : texto[..40] + "…";

    public static IEnumerable<Control> Descendientes(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            yield return hijo;
            foreach (var nieto in Descendientes(hijo))
            {
                yield return nieto;
            }
        }
    }
}
