using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Auditoria;

public sealed record CambioCampo(string Campo, string? Antes, string? Despues, bool Cambio);

public static class DiferenciasAuditoria
{
    private static readonly Regex RegexFechaUtc = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", RegexOptions.Compiled);

    public static IReadOnlyList<CambioCampo> Calcular(string? antesJson, string? despuesJson, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        if (string.IsNullOrWhiteSpace(antesJson) && string.IsNullOrWhiteSpace(despuesJson))
        {
            return Array.Empty<CambioCampo>();
        }

        JsonDocument? docAntes = null;
        JsonDocument? docDespues = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(antesJson))
            {
                try
                {
                    docAntes = JsonDocument.Parse(antesJson);
                    if (docAntes.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return new[] { new CambioCampo("(datos)", antesJson, despuesJson, true) };
                    }
                }
                catch (JsonException)
                {
                    return new[] { new CambioCampo("(datos)", antesJson, despuesJson, true) };
                }
            }

            if (!string.IsNullOrWhiteSpace(despuesJson))
            {
                try
                {
                    docDespues = JsonDocument.Parse(despuesJson);
                    if (docDespues.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return new[] { new CambioCampo("(datos)", antesJson, despuesJson, true) };
                    }
                }
                catch (JsonException)
                {
                    return new[] { new CambioCampo("(datos)", antesJson, despuesJson, true) };
                }
            }

            var camposOrdenados = new List<string>();
            var valoresAntes = new Dictionary<string, string?>(StringComparer.Ordinal);
            var valoresDespues = new Dictionary<string, string?>(StringComparer.Ordinal);

            if (docAntes != null)
            {
                foreach (var prop in docAntes.RootElement.EnumerateObject())
                {
                    camposOrdenados.Add(prop.Name);
                    valoresAntes[prop.Name] = TraducirValor(prop.Name, ExtraerValor(prop.Value, zona));
                }
            }

            if (docDespues != null)
            {
                foreach (var prop in docDespues.RootElement.EnumerateObject())
                {
                    if (!valoresAntes.ContainsKey(prop.Name))
                    {
                        camposOrdenados.Add(prop.Name);
                    }
                    valoresDespues[prop.Name] = TraducirValor(prop.Name, ExtraerValor(prop.Value, zona));
                }
            }

            var resultado = new List<CambioCampo>(camposOrdenados.Count);
            foreach (var campo in camposOrdenados)
            {
                valoresAntes.TryGetValue(campo, out var vAntes);
                valoresDespues.TryGetValue(campo, out var vDespues);

                bool cambio = !string.Equals(vAntes, vDespues, StringComparison.Ordinal);
                resultado.Add(new CambioCampo(campo, vAntes, vDespues, cambio));
            }

            return resultado;
        }
        finally
        {
            docAntes?.Dispose();
            docDespues?.Dispose();
        }
    }

    // F11: el tipo de pesaje se guarda como nombre del enum (SoloResCompleta); se muestra con el texto de la UI.
    private static string? TraducirValor(string campo, string? valor)
    {
        if (campo != "ModoDesposte")
        {
            return valor;
        }

        if (valor is null)
        {
            return ReglasDesposte.Texto(null);
        }

        return Enum.TryParse<ModoDesposte>(valor, out var modo) && ReglasDesposte.EsValido(modo)
            ? ReglasDesposte.Texto(modo)
            : valor;
    }

    private static string? ExtraerValor(JsonElement element, TimeZoneInfo zona)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;

            case JsonValueKind.String:
                var s = element.GetString();
                if (s != null && RegexFechaUtc.IsMatch(s))
                {
                    if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dtUtc))
                    {
                        var dtLocal = TimeZoneInfo.ConvertTimeFromUtc(dtUtc, zona);
                        return dtLocal.ToString("dd/MM/yyyy HH:mm:ss", TextosBoleta.Cultura);
                    }
                }
                return s;

            case JsonValueKind.Number:
                return element.GetRawText();

            case JsonValueKind.True:
                return "Sí";

            case JsonValueKind.False:
                return "No";

            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return element.GetRawText();

            default:
                return element.GetRawText();
        }
    }

    public static string EtiquetaCampo(string entidad, string campo)
    {
        if (string.Equals(entidad, "Pesaje", StringComparison.OrdinalIgnoreCase))
        {
            return campo switch
            {
                "NumeroBoleta" => "N° de boleta",
                "FechaHora" => "Fecha y hora",
                "GanaderoId" => "Ganadero (id)",
                // "MarcaGanadero" es la clave de los registros históricos (antes de F10 "Marca" pasó a "Código").
                "CodigoGanadero" or "MarcaGanadero" => "Ganadero (código)",
                "NombreGanadero" => "Ganadero (nombre)",
                "ClienteId" => "Cliente (id)",
                "CodigoCliente" => "Cliente (código)",
                "NombreCliente" => "Cliente (nombre)",
                "FleteroId" => "Fletero (id)",
                "CodigoFletero" => "Fletero (código)",
                "NombreFletero" => "Fletero (nombre)",
                "Direccion" => "Dirección",
                "Secuencia" => "Secuencia",
                "ColorRaza" => "Color/raza",
                "Sexo" => "Sexo",
                "TipoDesposte" => "Tipo de desposte",
                "PesoIzquierdo" => "Peso izquierdo",
                "PesoDerecho" => "Peso derecho",
                "PesoTotal" => "Peso total",
                "Observacion" => "Observación",
                "Estado" => "Estado",
                "OrigenPeso" => "Origen del peso",
                "UsuarioId" => "Usuario (id)",
                "MotivoAnulacion" => "Motivo de anulación",
                "AnuladoPorId" => "Anulado por (id)",
                "AnuladoEn" => "Anulado en",
                _ => campo
            };
        }

        if (string.Equals(entidad, "ParametrosSistema", StringComparison.OrdinalIgnoreCase) && campo == "ModoDesposte")
        {
            return "Tipo de pesaje";
        }

        // Los registros anteriores a F10 guardaron el código del ganadero como "Marca" y el del fletero como "Orden".
        if (string.Equals(entidad, "Ganadero", StringComparison.OrdinalIgnoreCase) && campo == "Marca")
        {
            return "Codigo";
        }

        if (string.Equals(entidad, "Fletero", StringComparison.OrdinalIgnoreCase) && campo == "Orden")
        {
            return "Codigo";
        }

        return campo;
    }
}
