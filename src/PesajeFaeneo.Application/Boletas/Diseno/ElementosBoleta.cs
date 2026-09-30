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

public sealed record PaginaBoleta(
    float AnchoMm,
    float AltoMm,
    IReadOnlyList<ElementoBoleta> Elementos);
