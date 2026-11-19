using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Infrastructure.Licencias;

/// <summary>
/// F13 (D2): estado local de la licencia en <c>HKCU\Software\Classes\CLSID\{...}\Cfg</c> (fuera de la carpeta de datos, para
/// que sobreviva a reinstalar). Valores REG_SZ: d (inicio), l (última vez visto), s (firma) y a (token de activación).
/// La subclave es inyectable para que las pruebas usen una temporal.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AlmacenLicenciaRegistro : IAlmacenLicenciaLocal
{
    private readonly string _subclave;

    public AlmacenLicenciaRegistro() : this(ConstantesLicencia.SubclaveRegistro)
    {
    }

    public AlmacenLicenciaRegistro(string subclave)
    {
        if (string.IsNullOrWhiteSpace(subclave))
        {
            throw new ArgumentException("La subclave es obligatoria.", nameof(subclave));
        }

        _subclave = subclave;
    }

    public DatosLicenciaLocal Leer()
    {
        try
        {
            using var clave = Registry.CurrentUser.OpenSubKey(_subclave, writable: false);
            if (clave is null)
            {
                return DatosLicenciaLocal.Vacio;
            }

            return new DatosLicenciaLocal(
                LeerTexto(clave, ConstantesLicencia.ValorInicio),
                LeerTexto(clave, ConstantesLicencia.ValorUltimo),
                LeerTexto(clave, ConstantesLicencia.ValorFirma),
                LeerTexto(clave, ConstantesLicencia.ValorActivacion));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return DatosLicenciaLocal.Vacio;
        }
    }

    public void EscribirPrueba(long inicio, long ultimo, string firma)
    {
        ArgumentNullException.ThrowIfNull(firma);
        using var clave = Abrir();
        clave.SetValue(ConstantesLicencia.ValorInicio, inicio.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
        clave.SetValue(ConstantesLicencia.ValorUltimo, ultimo.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
        clave.SetValue(ConstantesLicencia.ValorFirma, firma, RegistryValueKind.String);
    }

    public void EscribirActivacion(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        using var clave = Abrir();
        clave.SetValue(ConstantesLicencia.ValorActivacion, token, RegistryValueKind.String);
    }

    public void LimpiarActivacion() => EscribirActivacion(string.Empty);

    private RegistryKey Abrir() =>
        Registry.CurrentUser.CreateSubKey(_subclave, writable: true)
        ?? throw new IOException("No se pudo abrir la clave de registro de la licencia.");

    private static string? LeerTexto(RegistryKey clave, string nombre)
    {
        // Solo REG_SZ: un valor de otro tipo se trata como ausente.
        if (clave.GetValue(nombre, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string texto
            && clave.GetValueKind(nombre) == RegistryValueKind.String)
        {
            return texto;
        }

        return null;
    }
}
