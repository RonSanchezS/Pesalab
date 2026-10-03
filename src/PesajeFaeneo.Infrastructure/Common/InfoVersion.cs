using System.Reflection;

namespace PesajeFaeneo.Infrastructure.Common;

/// <summary>
/// Versión de la aplicación (D1). Única fuente: &lt;Version&gt; de Directory.Build.props, que comparten todos los
/// ensamblados de la solución. Informativa = Version + "+sha" (lo agrega el SDK dentro de git), con el sha recortado.
/// </summary>
public static class InfoVersion
{
    public const string Producto = "Sistema de Pesaje para Faeneo";

    public static string Version { get; } = typeof(InfoVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static string Informativa { get; } = RecortarInformativa(
        typeof(InfoVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        Version);

    internal static string RecortarInformativa(string? valor, string respaldo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return respaldo;
        }

        var mas = valor.IndexOf('+');
        if (mas < 0 || valor.Length - mas - 1 <= 7)
        {
            return valor;
        }

        return valor[..(mas + 8)];
    }
}
