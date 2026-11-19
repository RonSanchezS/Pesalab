namespace PesajeFaeneo.Application.Boletas.Diseno;

public enum AlineacionTexto
{
    Izquierda,
    Centro,
    Derecha
}

public enum TonoTinta
{
    Negro,
    Gris
}

public abstract record ElementoBoleta;

public sealed record TextoBoleta(
    float X,
    float Y,
    float Ancho,
    float Alto,
    string Texto,
    float TamanoPt,
    bool Negrita = false,
    AlineacionTexto Alineacion = AlineacionTexto.Izquierda,
    bool VariasLineas = false,
    TonoTinta Tono = TonoTinta.Negro) : ElementoBoleta;

public sealed record LineaBoleta(
    float X1,
    float Y1,
    float X2,
    float Y2,
    float GrosorMm = 0.3f) : ElementoBoleta;

public sealed record RectanguloBoleta(
    float X,
    float Y,
    float Ancho,
    float Alto,
    float GrosorMm = 0.3f) : ElementoBoleta;

public sealed record LogoBoleta(
    float X,
    float Y,
    float Ancho,
    float Alto) : ElementoBoleta;

/// <summary>Logo de la empresa atenuado, dibujado detrás de todo el contenido (marca de agua).</summary>
public sealed record FondoLogoBoleta(
    float X,
    float Y,
    float Ancho,
    float Alto,
    float Opacidad) : ElementoBoleta;

public sealed record PaginaBoleta(
    float AnchoMm,
    float AltoMm,
    IReadOnlyList<ElementoBoleta> Elementos);
