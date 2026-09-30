namespace PesajeFaeneo.Application.Boletas.Dtos;

public enum FormatoBoleta
{
    MediaCarta = 1,
    Ticket80mm = 2
}

public sealed record ConfiguracionImpresion(
    string? NombreImpresora,           // null = predeterminada de Windows
    FormatoBoleta Formato,
    string? NombrePapel,               // null = automático (7.4)
    int Copias,
    bool ImprimirAlGrabar,
    decimal DesplazamientoXMm,
    decimal DesplazamientoYMm);

public sealed record ConfiguracionImpresionEquipo(
    ConfiguracionImpresion Configuracion,
    bool Configurada,                  // false = se usan los valores por defecto (D6)
    string NombreEquipo);
