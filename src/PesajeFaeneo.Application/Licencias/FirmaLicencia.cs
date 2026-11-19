using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PesajeFaeneo.Application.Licencias;

/// <summary>
/// F13 (D2): fórmulas SHA-256 del esquema local de licencias. Puras y sin E/S. Todas devuelven hexadecimal en minúsculas.
/// </summary>
public static class FirmaLicencia
{
    /// <summary>Sal propia de Capataz (distinta de la de ScaleLogger).</summary>
    public const string Sal = "pf-86372349-fb7c-4def-8add-f9b6b87ad2c2";

    public static string Sha256Hex(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
    }

    /// <summary>Identificador del equipo: <c>SHA256("hw|{guid en minúsculas}|{sal}")</c>.</summary>
    public static string Hwid(string machineGuid)
    {
        ArgumentNullException.ThrowIfNull(machineGuid);
        return Sha256Hex($"hw|{machineGuid.Trim().ToLowerInvariant()}|{Sal}");
    }

    /// <summary>Firma de la prueba: <c>SHA256("trial|{d}|{l}|{hwid}|{sal}")</c> con d = inicio y l = última vez visto (epoch).</summary>
    public static string Prueba(long inicio, long ultimo, string hwid)
    {
        ArgumentNullException.ThrowIfNull(hwid);
        return Sha256Hex(string.Create(CultureInfo.InvariantCulture, $"trial|{inicio}|{ultimo}|{hwid}|{Sal}"));
    }

    /// <summary>Token de activación local: <c>SHA256("act|{hwid}|{sal}")</c>.</summary>
    public static string TokenActivacion(string hwid)
    {
        ArgumentNullException.ThrowIfNull(hwid);
        return Sha256Hex($"act|{hwid}|{Sal}");
    }
}
