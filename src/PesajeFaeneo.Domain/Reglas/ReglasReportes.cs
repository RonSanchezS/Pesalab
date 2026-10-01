using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Domain.Reglas;

public static class CamposReportes
{
    public const string Tipo = "Tipo";
    public const string FechaDesde = "FechaDesde";
    public const string FechaHasta = "FechaHasta";
    public const string Ganadero = "Ganadero";
    public const string Cliente = "Cliente";
}

public static class ReglasReportes
{
    public const int MaximoFilas = 20_000;
    public const int MaximoFilasVista = 1_000;
    public const int MaximoFilasImpresion = 2_000;
    public const int ResolucionImpresionDpi = 200;

    // HU-F7-01: una res completa (dos medias reses pesadas) cuenta 1 animal; una media res, 0,5.
    // Única fuente de la regla: cambiarla (P1) es cambiar esta función y sus tests.
    public static decimal Reses(TipoDesposte tipo) => tipo switch
    {
        TipoDesposte.ResCompleta => 1.0m,
        TipoDesposte.MediaRes => 0.5m,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de desposte desconocido.")
    };
}
