namespace PesajeFaeneo.Application.Utilitarios.Backup;

using System.Globalization;
using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;

public static class CamposBackup
{
    public const string Archivo = "Archivo";
    public const string DiasRetencion = "DiasRetencion";
    public const string CarpetaSecundaria = "CarpetaSecundaria";
    public const string Confirmacion = "Confirmacion";
}

public static class ReglasBackup
{
    public const int FormatoManifiesto = 1;
    public const string NombreAplicacion = "PesajeFaeneo";
    public const string EntradaBaseDatos = "pesaje.db";
    public const string EntradaManifiesto = "manifest.json";
    public const string PrefijoLogo = "logo/";
    public const int MaximoArchivosLogo = 20;
    public const int TamanoMaximoLogo = 1_048_576;
    public const long TamanoMaximoBackup = 2L * 1024 * 1024 * 1024;
    public const int TamanoMaximoManifiesto = 65_536;
    public const int DiasRetencionMinimo = 7;
    public const int DiasRetencionMaximo = 365;
    public const int DiasRetencionPorDefecto = 30;
    public const int MinimoAutomaticosConservados = 3;
    public const int SeguridadConservadosPorTipo = 10;
    public const int ReemplazadasConservadas = 3;
    public const string ConfirmacionRestauracion = "RESTAURAR";
    public const string CarpetaAutomaticos = "automaticos";
    public const string CarpetaManuales = "manuales";
    public const string CarpetaSeguridad = "seguridad";
    public const int MaximoListado = 500;

    public static readonly IReadOnlySet<string> ExtensionesLogo = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp"
    };

    private static readonly Regex RegexAutomatico = new(
        @"^PesajeFaeneo_automatico_(\d{4}-\d{2}-\d{2})\.zip$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NombreArchivo(TipoBackup tipo, DateTime local)
    {
        return tipo switch
        {
            TipoBackup.Automatico => $"PesajeFaeneo_automatico_{local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.zip",
            TipoBackup.Manual => $"PesajeFaeneo_manual_{local.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.zip",
            TipoBackup.PreRestauracion => $"PesajeFaeneo_prerestauracion_{local.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.zip",
            TipoBackup.PreImportacion => $"PesajeFaeneo_preimportacion_{local.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}.zip",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de backup no válido.")
        };
    }

    public static string Carpeta(TipoBackup tipo)
    {
        return tipo switch
        {
            TipoBackup.Automatico => CarpetaAutomaticos,
            TipoBackup.Manual => CarpetaManuales,
            TipoBackup.PreRestauracion => CarpetaSeguridad,
            TipoBackup.PreImportacion => CarpetaSeguridad,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de backup no válido.")
        };
    }

    public static bool TryLeerFechaAutomatico(string nombre, out DateOnly fecha)
    {
        fecha = default;
        var match = RegexAutomatico.Match(nombre);
        if (!match.Success)
        {
            return false;
        }

        return DateOnly.TryParseExact(
            match.Groups[1].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fecha);
    }

    public static IReadOnlyList<string> SeleccionarAutomaticosAEliminar(
        IEnumerable<string> nombres,
        int diasRetencion,
        DateOnly hoyLocal)
    {
        var validos = new List<(string Nombre, DateOnly Fecha)>();
        foreach (var n in nombres)
        {
            if (TryLeerFechaAutomatico(n, out var fecha))
            {
                validos.Add((n, fecha));
            }
        }

        if (validos.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ordenados = validos.OrderByDescending(x => x.Fecha).ToList();
        var fechaLimite = hoyLocal.AddDays(-(diasRetencion - 1));

        var aEliminar = new List<string>();
        for (int i = 0; i < ordenados.Count; i++)
        {
            // Siempre conservar los MinimoAutomaticosConservados más recientes
            if (i < MinimoAutomaticosConservados)
            {
                continue;
            }

            // Conservar si fecha >= fechaLimite
            if (ordenados[i].Fecha >= fechaLimite)
            {
                continue;
            }

            aEliminar.Add(ordenados[i].Nombre);
        }

        return aEliminar;
    }

    public static IReadOnlyList<string> SeleccionarSeguridadAEliminar(
        IEnumerable<string> nombres,
        TipoBackup tipo)
    {
        var prefijo = tipo switch
        {
            TipoBackup.PreRestauracion => "PesajeFaeneo_prerestauracion_",
            TipoBackup.PreImportacion => "PesajeFaeneo_preimportacion_",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de seguridad no válido.")
        };

        var coincidentes = nombres
            .Where(n => n.StartsWith(prefijo, StringComparison.Ordinal) && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .ToList();

        if (coincidentes.Count <= SeguridadConservadosPorTipo)
        {
            return Array.Empty<string>();
        }

        return coincidentes.Skip(SeguridadConservadosPorTipo).ToList();
    }

    public static IReadOnlyList<ErrorValidacion> ValidarConfiguracion(ConfiguracionBackup c)
    {
        var errores = new List<ErrorValidacion>();
        if (c.DiasRetencion < DiasRetencionMinimo || c.DiasRetencion > DiasRetencionMaximo)
        {
            errores.Add(new ErrorValidacion(CamposBackup.DiasRetencion, "Indique entre 7 y 365 días."));
        }

        if (!string.IsNullOrWhiteSpace(c.CarpetaSecundaria))
        {
            var ruta = c.CarpetaSecundaria.Trim();
            if (!Path.IsPathFullyQualified(ruta) || ruta.Length > 240)
            {
                errores.Add(new ErrorValidacion(CamposBackup.CarpetaSecundaria, @"Indique una carpeta completa (por ejemplo E:\Backups)."));
            }
        }

        return errores;
    }

    public static string TextoTipo(TipoBackup tipo)
    {
        return tipo switch
        {
            TipoBackup.Manual => "Manual",
            TipoBackup.Automatico => "Automático",
            TipoBackup.PreRestauracion => "Resguardo previo a restaurar",
            TipoBackup.PreImportacion => "Resguardo previo a importar",
            _ => "Desconocido"
        };
    }
}
