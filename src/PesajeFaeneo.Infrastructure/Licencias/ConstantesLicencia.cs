using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Infrastructure.Licencias;

/// <summary>
/// F13 (D2, D8): constantes de código, no configuración: editar un archivo no debe poder redirigir las licencias a otro
/// servidor. La clave anon es pública por diseño (la seguridad está en RLS y en las funciones RPC); jamás va la service_role.
/// </summary>
internal static class ConstantesLicencia
{
    public const string UrlServidor = "https://uxzehuncusnaofmfjydk.supabase.co";

    public const string ClaveAnon = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InV4emVodW5jdXNuYW9mbWZqeWRrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTE0MTk4MzAsImV4cCI6MjEwNjk5NTgzMH0.cjtESAxgehPEJz6fSdgIyxzM8-J6qNVziXz7fZZlTVM";

    /// <summary>Producto de esta aplicación en la base compartida con ScaleLogger.</summary>
    public const string Producto = "pesajefaeneo";

    public const string Sal = FirmaLicencia.Sal;

    /// <summary>Ubicación poco obvia en HKCU; la escribe la aplicación (no el instalador), por eso sobrevive a reinstalar.</summary>
    public const string SubclaveRegistro = @"Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg";

    public const string ValorInicio = "d";
    public const string ValorUltimo = "l";
    public const string ValorFirma = "s";
    public const string ValorActivacion = "a";

    public const int TiempoEsperaSegundos = 8;
}
