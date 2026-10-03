using System.Security.Cryptography;

namespace PesajeFaeneo.Application.Seguridad;

/// <summary>Contraseña temporal aleatoria para --restablecer-admin (D17). Sin caracteres ambiguos (0/O, 1/l/I).</summary>
public static class GeneradorPasswordTemporal
{
    internal const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public static string Generar(int longitud = 10)
    {
        if (longitud < PoliticaSeguridad.LongitudMinimaPassword || longitud > PoliticaSeguridad.LongitudMaximaPassword)
        {
            throw new ArgumentOutOfRangeException(nameof(longitud));
        }

        var caracteres = new char[longitud];
        for (var i = 0; i < longitud; i++)
        {
            caracteres[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        return new string(caracteres);
    }
}
