using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Balanza;

public sealed class ParserTramas
{
    private readonly Regex _regex;
    private readonly bool _tieneGrupoEstable;
    private readonly UnidadPesoTrama _unidad;

    public ParserTramas(string patron, UnidadPesoTrama unidad = UnidadPesoTrama.Kilogramos)
    {
        if (!Enum.IsDefined(unidad))
        {
            throw new ArgumentOutOfRangeException(nameof(unidad), unidad, "Unidad de peso no soportada.");
        }

        _unidad = unidad;

        var error = ValidarPatron(patron);
        if (error != null)
        {
            throw new ArgumentException(error, nameof(patron));
        }

        _regex = new Regex(
            patron,
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(ReglasBalanza.TiempoLimiteRegexMs));

        _tieneGrupoEstable = _regex.GetGroupNames().Contains(ReglasBalanza.GrupoEstable);
    }

    public static string? ValidarPatron(string? patron)
    {
        if (string.IsNullOrWhiteSpace(patron))
        {
            return "Ingrese el patrón de lectura.";
        }

        if (patron.Length > ReglasBalanza.LongitudMaximaPatron)
        {
            return "El patrón no puede superar 250 caracteres.";
        }

        Regex regex;
        try
        {
            regex = new Regex(
                patron,
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(ReglasBalanza.TiempoLimiteRegexMs));
        }
        catch (ArgumentException)
        {
            return "El patrón no es una expresión regular válida.";
        }

        if (!regex.GetGroupNames().Contains(ReglasBalanza.GrupoPeso))
        {
            return "El patrón debe tener un grupo con nombre (?<peso>...).";
        }

        return null;
    }

    public ResultadoTrama Parsear(string? trama)
    {
        if (string.IsNullOrEmpty(trama))
        {
            return ResultadoTrama.Invalida("Trama vacía.");
        }

        Match match;
        try
        {
            match = _regex.Match(trama);
        }
        catch (RegexMatchTimeoutException)
        {
            return ResultadoTrama.Invalida("La trama tardó demasiado en analizarse.");
        }

        if (!match.Success)
        {
            return ResultadoTrama.Invalida("La trama no contiene un peso reconocible.");
        }

        var grupoPeso = match.Groups[ReglasBalanza.GrupoPeso];
        if (!grupoPeso.Success || string.IsNullOrWhiteSpace(grupoPeso.Value))
        {
            return ResultadoTrama.Invalida("La trama no contiene un peso reconocible.");
        }

        // Si inmediatamente después del grupo peso continúa con separador y dígitos (p. ej. 1,234.5),
        // se trata de un número con múltiples separadores (R12), no de un número válido.
        int finGrupo = grupoPeso.Index + grupoPeso.Length;
        if (finGrupo < trama.Length)
        {
            char siguiente = trama[finGrupo];
            if ((siguiente == '.' || siguiente == ',') && finGrupo + 1 < trama.Length && char.IsDigit(trama[finGrupo + 1]))
            {
                return ResultadoTrama.Invalida("El peso de la trama no es un número válido.");
            }
        }

        var sb = new StringBuilder(grupoPeso.Value.Length);
        foreach (var c in grupoPeso.Value)
        {
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(c == ',' ? '.' : c);
            }
        }
        var textoPeso = sb.ToString();

        if (!decimal.TryParse(
            textoPeso,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var peso))
        {
            return ResultadoTrama.Invalida("El peso de la trama no es un número válido.");
        }

        if (_unidad == UnidadPesoTrama.Gramos)
        {
            peso /= 1000m;
        }

        peso = Math.Round(peso, 1, MidpointRounding.AwayFromZero);
        if (Math.Abs(peso) > ReglasBalanza.PesoMaximoAbsolutoKg)
        {
            return ResultadoTrama.Invalida("El peso está fuera del rango admitido.");
        }

        bool? estableReportado = null;
        if (_tieneGrupoEstable)
        {
            var grupoEstable = match.Groups[ReglasBalanza.GrupoEstable];
            estableReportado = grupoEstable.Success && grupoEstable.Length > 0;
        }

        return new ResultadoTrama(true, peso, estableReportado, null);
    }
}
