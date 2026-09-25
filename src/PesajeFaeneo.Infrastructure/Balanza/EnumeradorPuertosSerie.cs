using System.IO.Ports;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Infrastructure.Balanza;

public class EnumeradorPuertosSerie : IEnumeradorPuertosSerie
{
    private readonly ILogger<EnumeradorPuertosSerie> _logger;

    public EnumeradorPuertosSerie(ILogger<EnumeradorPuertosSerie> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<string> Listar()
    {
        string[] nombres;
        try
        {
            nombres = SerialPort.GetPortNames();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al enumerar puertos serie del sistema.");
            return Array.Empty<string>();
        }

        return Normalizar(nombres);
    }

    internal static IReadOnlyList<string> Normalizar(IEnumerable<string> nombres)
    {
        if (nombres == null)
        {
            return Array.Empty<string>();
        }

        return nombres
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToUpperInvariant())
            .Where(n => Regex.IsMatch(n, ReglasBalanza.PatronPuertoCom))
            .Distinct()
            .OrderBy(n => int.TryParse(n[3..], out var num) ? num : int.MaxValue)
            .ToList();
    }
}
