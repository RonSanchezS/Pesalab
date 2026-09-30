using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas.Diseno;

public static class DisenoCalibracion
{
    public const float MargenReferenciaMm = 10f;

    public static PaginaBoleta Crear(FormatoBoleta formato)
    {
        float ancho = formato == FormatoBoleta.MediaCarta ? 216f : 80f;
        float alto = formato == FormatoBoleta.MediaCarta ? 140f : 120f;

        var elementos = new List<ElementoBoleta>();

        // Rectángulo a 10 mm de cada borde
        elementos.Add(new RectanguloBoleta(10f, 10f, ancho - 20f, alto - 20f, 0.4f));

        // Reglas cada 10 mm en borde superior (líneas verticales de 3 mm desde Y = 10)
        for (float x = 20f; x <= ancho - 20f; x += 10f)
        {
            elementos.Add(new LineaBoleta(x, 10f, x, 13f, 0.2f));
            elementos.Add(new TextoBoleta(x - 5f, 13.5f, 10f, 3.5f, ((int)x).ToString(TextosBoleta.Cultura), 7f, Alineacion: AlineacionTexto.Centro));
        }

        // Reglas cada 10 mm en borde izquierdo (líneas horizontales de 3 mm desde X = 10)
        for (float y = 20f; y <= alto - 20f; y += 10f)
        {
            elementos.Add(new LineaBoleta(10f, y, 13f, y, 0.2f));
            elementos.Add(new TextoBoleta(13.5f, y - 2f, 10f, 4f, ((int)y).ToString(TextosBoleta.Cultura), 7f, Alineacion: AlineacionTexto.Izquierda));
        }

        // Cruz central (dos líneas de 10 mm)
        float cx = ancho / 2f;
        float cy = alto / 2f;
        elementos.Add(new LineaBoleta(cx - 5f, cy, cx + 5f, cy, 0.3f));
        elementos.Add(new LineaBoleta(cx, cy - 5f, cx, cy + 5f, 0.3f));

        // Textos centrados (9 pt). Van a la derecha de los números de la regla izquierda
        // (X 13,5–23,5 mm) para no pisarlos, y en varias líneas para que no se corten con "…".
        if (formato == FormatoBoleta.MediaCarta)
        {
            elementos.Add(new TextoBoleta(25f, 30f, ancho - 50f, 6f, "PÁGINA DE CALIBRACIÓN", 11f, Negrita: true, Alineacion: AlineacionTexto.Centro));
            elementos.Add(new TextoBoleta(25f, 40f, ancho - 50f, 5f, "El recuadro debe quedar a 10 mm de cada borde del papel.", 9f, Alineacion: AlineacionTexto.Centro));
            elementos.Add(new TextoBoleta(25f, 47f, ancho - 50f, 10f, "Si queda a más de 10 mm del borde izquierdo, reste la diferencia al desplazamiento X; si queda a menos, súmela.", 9f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            elementos.Add(new TextoBoleta(25f, 58f, ancho - 50f, 5f, "Igual con el borde superior y el desplazamiento Y.", 9f, Alineacion: AlineacionTexto.Centro));
        }
        else
        {
            elementos.Add(new TextoBoleta(25f, 20f, ancho - 35f, 10f, "PÁGINA DE CALIBRACIÓN", 10f, Negrita: true, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            elementos.Add(new TextoBoleta(25f, 31f, ancho - 35f, 14f, "El recuadro debe quedar a 10 mm de cada borde del papel.", 9f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            elementos.Add(new TextoBoleta(25f, 66f, ancho - 35f, 28f, "Si queda a más de 10 mm del borde izquierdo, reste la diferencia al desplazamiento X; si queda a menos, súmela.", 9f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            elementos.Add(new TextoBoleta(25f, 95f, ancho - 35f, 14f, "Igual con el borde superior y el desplazamiento Y.", 9f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
        }

        return new PaginaBoleta(ancho, alto, elementos);
    }
}
