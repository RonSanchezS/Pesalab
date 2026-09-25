namespace PesajeFaeneo.Application.Parametros;

public static class CamposParametros
{
    public const string NombreEmpresa = "NombreEmpresa";
    public const string Direccion = "Direccion";
    public const string Telefono = "Telefono";
    public const string Ciudad = "Ciudad";
    public const string RegistroSanitario = "RegistroSanitario";
    public const string TelefonosReclamos = "TelefonosReclamos";
    public const string ObservacionPorDefecto = "ObservacionPorDefecto";
    public const string SiguienteNumeroBoleta = "SiguienteNumeroBoleta";
    public const string Logo = "Logo";
}

public static class ReglasParametros
{
    public const int LongitudMinimaNombreEmpresa = 3;
    public const int LongitudMaximaNombreEmpresa = 100;
    public const int LongitudMaximaDireccion = 150;
    public const int LongitudMaximaTelefono = 50;
    public const int LongitudMaximaCiudad = 100;
    public const int LongitudMaximaRegistroSanitario = 100;
    public const int LongitudMaximaTelefonosReclamos = 100;
    public const int LongitudMaximaObservacionPorDefecto = 250;
    public const long SiguienteNumeroBoletaMinimo = 1;
    public const long SiguienteNumeroBoletaMaximo = 999_999_999;
}
