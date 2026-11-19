using System.Runtime.Versioning;
using Microsoft.Win32;
using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Infrastructure.Licencias;

/// <summary>
/// F13 (D2): identificador del equipo a partir del MachineGuid de Windows (vista de 64 bits). Si no se puede leer se usa el
/// nombre del equipo en minúsculas, nunca la cadena vacía.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IdentificadorEquipoWindows : IIdentificadorEquipo
{
    private readonly Lazy<string> _hwid;

    public IdentificadorEquipoWindows()
    {
        _hwid = new Lazy<string>(Calcular);
    }

    public string Hwid => _hwid.Value;

    private static string Calcular() => FirmaLicencia.Hwid(LeerMachineGuid());

    internal static string LeerMachineGuid()
    {
        try
        {
            using var @base = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var clave = @base.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
            if (clave?.GetValue("MachineGuid") is string guid && !string.IsNullOrWhiteSpace(guid))
            {
                return guid.Trim().ToLowerInvariant();
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Se usa el respaldo.
        }

        var nombre = Environment.MachineName.Trim().ToLowerInvariant();
        return nombre.Length > 0 ? nombre : "equipo-desconocido";
    }
}
