namespace PesajeFaeneo.Domain.Reglas;

public static class CamposGestionPesajes
{
    public const string Motivo = "Motivo";
    public const string FechaDesde = "FechaDesde";
    public const string FechaHasta = "FechaHasta";
    public const string NumeroBoleta = "NumeroBoleta";
}

public static class ReglasGestionPesajes
{
    public const int MotivoAnulacionMinimo = 5;
    public const int MotivoAnulacionMaximo = 100;
    public const int MotivoEdicionMaximo = 100;
    public const int BusquedaMaximoFilas = 200;
    public const int AuditoriaMaximoFilas = 500;
    public const int MotivoLeyendaMaximo = 30;
}
