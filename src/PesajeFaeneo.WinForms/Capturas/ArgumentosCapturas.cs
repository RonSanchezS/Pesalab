#if CAPTURAS_MANUAL
using System.Text.Json;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>Argumentos del build de capturas: --datos &lt;carpeta&gt; [--capturas &lt;salida&gt;] [--config &lt;json&gt;] [--solo &lt;prefijo[,prefijo]&gt;].</summary>
internal sealed record ArgumentosCapturas(string Datos, string? Salida, string? Config, IReadOnlyList<string> Solo, bool SinPesajes = false)
{
    public const string ArgDatos = "--datos";
    public const string ArgCapturas = "--capturas";
    public const string ArgConfig = "--config";
    public const string ArgSolo = "--solo";
    public const string ArgSinPesajes = "--sin-pesajes";

    public static ArgumentosCapturas? Leer(string[] args)
    {
        var datos = Valor(args, ArgDatos);
        if (string.IsNullOrWhiteSpace(datos))
        {
            return null;
        }

        var solo = (Valor(args, ArgSolo) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sinPesajes = args.Any(a => string.Equals(a, ArgSinPesajes, StringComparison.OrdinalIgnoreCase));
        return new ArgumentosCapturas(Path.GetFullPath(datos), Valor(args, ArgCapturas), Valor(args, ArgConfig), solo, sinPesajes);
    }

    /// <summary>Quita de la línea de comandos los argumentos propios (el Host los tomaría como claves de configuración).</summary>
    public static string[] QuitarDe(string[] args)
    {
        var resultado = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is ArgDatos or ArgCapturas or ArgConfig or ArgSolo)
            {
                i++;
                continue;
            }

            if (string.Equals(args[i], ArgSinPesajes, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resultado.Add(args[i]);
        }

        return resultado.ToArray();
    }

    private static string? Valor(string[] args, string nombre)
    {
        var i = Array.FindIndex(args, a => string.Equals(a, nombre, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

internal sealed record UsuarioDemo(string NombreUsuario, string NombreCompleto, Rol Rol, string Password, bool DebeCambiar);

/// <summary>Credenciales de prueba del build de capturas (archivo scripts/capturas-manual.config.json; nunca van en los manuales).</summary>
internal sealed class ConfigDemo
{
    public required string PasswordAdmin { get; init; }
    public string NombreCompletoAdmin { get; init; } = "CARLOS ROJAS";
    public required IReadOnlyList<UsuarioDemo> Usuarios { get; init; }

    /// <summary>Datos sembrados sin ningún pesaje (para mostrar las pantallas antes de la primera boleta).</summary>
    public bool SinPesajes { get; set; }

    public UsuarioDemo Operador => Usuarios.First(u => u.NombreUsuario == "operador");
    public UsuarioDemo Supervisor => Usuarios.First(u => u.NombreUsuario == "supervisor");
    public UsuarioDemo Nuevo => Usuarios.First(u => u.DebeCambiar);

    public static ConfigDemo Cargar(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            throw new FileNotFoundException("Falta el archivo de configuración de capturas (--config).", ruta);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
        var raiz = doc.RootElement;
        var admin = raiz.GetProperty("admin");
        var usuarios = new List<UsuarioDemo>();
        foreach (var u in raiz.GetProperty("usuarios").EnumerateArray())
        {
            usuarios.Add(new UsuarioDemo(
                u.GetProperty("nombreUsuario").GetString()!,
                u.GetProperty("nombreCompleto").GetString()!,
                Enum.Parse<Rol>(u.GetProperty("rol").GetString()!),
                u.GetProperty("password").GetString()!,
                u.TryGetProperty("debeCambiar", out var d) && d.GetBoolean()));
        }

        return new ConfigDemo
        {
            PasswordAdmin = admin.GetProperty("password").GetString()!,
            NombreCompletoAdmin = admin.GetProperty("nombreCompleto").GetString()!,
            Usuarios = usuarios
        };
    }
}

/// <summary>Reloj del build de capturas: igual al del sistema más un desplazamiento (para sembrar pesajes de días anteriores).</summary>
internal sealed class RelojDemo : TimeProvider
{
    public static RelojDemo Instancia { get; } = new();

    public TimeSpan Desplazamiento { get; set; } = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Desplazamiento;
}
#endif
