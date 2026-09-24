namespace PesajeFaeneo.Application.Seguridad;

public static class PoliticaSeguridad
{
    public const int LongitudMinimaPassword = 6;
    public const int LongitudMaximaPassword = 100;
    public const int MaximoIntentosFallidos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);
    public const string PatronNombreUsuario = "^[a-z0-9._-]{3,30}$";
}
