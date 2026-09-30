using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas;

public static class CamposImpresion
{
    public const string Impresora = "Impresora";
    public const string Formato = "Formato";
    public const string Papel = "Papel";
    public const string Copias = "Copias";
    public const string DesplazamientoX = "DesplazamientoX";
    public const string DesplazamientoY = "DesplazamientoY";
}

public static class CamposBoleta
{
    public const string Numero = "Numero";
}

public static class ReglasImpresion
{
    public const int CopiasMinimas = 1;
    public const int CopiasMaximas = 3;
    public const decimal DesplazamientoMaximoMm = 20.0m;       // rango [-20, +20]
    public const int LongitudMaximaNombreImpresora = 200;
    public const int LongitudMaximaNombrePapel = 100;
    public const string TextoImpresoraPredeterminada = "(predeterminada)";

    public static readonly ConfiguracionImpresion PorDefecto =
        new(null, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);
}
