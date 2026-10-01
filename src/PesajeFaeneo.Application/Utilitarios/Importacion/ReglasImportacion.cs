namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class ReglasImportacion
{
    public const long MaximoBytesCsvDbf = 100L * 1024 * 1024;
    public const long MaximoBytesAccess = 500L * 1024 * 1024;
    public const int MaximoFilas = 300_000;
    public const int MaximoColumnas = 255;
    public const int FilasVistaPrevia = 20;
    public const int MaximoIncidencias = 5_000;
    public const int TamanoLote = 1_000;
    public const int PaginaPorDefecto = 1252;
    public static readonly IReadOnlySet<int> PaginasPermitidas = new HashSet<int> { 65001, 1252, 850, 437 };
    public const decimal PesoMaximoLadoImportacionKg = 1200.0m;
    public const int AnioMinimo = 1990;
    public const string MotivoAnulacionPorDefecto = "ANULADA EN EL SISTEMA ANTERIOR";
}

public static class CamposOrigen
{
    public const string Archivo = "Archivo";
    public const string Tabla = "Tabla";
}
