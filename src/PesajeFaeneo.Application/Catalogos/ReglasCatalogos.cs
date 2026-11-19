namespace PesajeFaeneo.Application.Catalogos;

public static class ReglasCatalogos
{
    // Código del ganadero: texto de 1 a 5 caracteres. Los que asigna el sistema son numéricos correlativos; los
    // alfanuméricos (p. ej. "AH") vienen del sistema legado y solo entran por la importación.
    public const int LongitudMaximaCodigoGanadero = 5;
    public const string PatronCodigoGanadero = @"^[\p{Lu}\p{Nd}&./-]{1,5}$";
    public const int CodigoGanaderoNumericoMaximo = 99_999;
    public const int LongitudMinimaNombre = 3;
    public const int LongitudMaximaNombre = 100;
    public const int LongitudMaximaDireccion = 150;
    public const int CodigoClienteMinimo = 1;
    public const int CodigoClienteMaximo = 999_999;
    public const int LongitudMinimaCelular = 6;
    public const int LongitudMaximaCelular = 20;
    public const string PatronCelular = @"^[0-9+()\- ]{6,20}$";
    public const int DigitosMinimosCelular = 6;
}

public static class CamposCatalogo
{
    public const string Nombre = "Nombre";
    public const string Codigo = "Codigo";
    public const string Direccion = "Direccion";
    public const string Celular = "Celular";
}
