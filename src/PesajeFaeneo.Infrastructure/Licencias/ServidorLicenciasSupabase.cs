using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Infrastructure.Licencias;

/// <summary>
/// F13 (D8): cliente de las funciones RPC de Supabase (<c>POST {url}/rest/v1/rpc/{función}</c>) con un <see cref="HttpClient"/>
/// propio y 8 s de espera. Las funciones son las del producto (<c>*_product_*</c>): una clave de otro producto responde
/// "invalid". La clave y el equipo jamás se registran.
/// </summary>
public sealed class ServidorLicenciasSupabase : IServidorLicencias, IDisposable
{
    private const string MensajeSinConexion = "No se pudo contactar al servidor de licencias. Revise la conexión a internet.";

    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _claveAnon;
    private readonly string _producto;

    public ServidorLicenciasSupabase()
        : this(new HttpClientHandler(), ConstantesLicencia.UrlServidor, ConstantesLicencia.ClaveAnon, ConstantesLicencia.Producto,
            TimeSpan.FromSeconds(ConstantesLicencia.TiempoEsperaSegundos))
    {
    }

    internal ServidorLicenciasSupabase(HttpMessageHandler handler, string url, string claveAnon, string producto, TimeSpan tiempoEspera)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _url = (url ?? throw new ArgumentNullException(nameof(url))).TrimEnd('/');
        _claveAnon = claveAnon ?? throw new ArgumentNullException(nameof(claveAnon));
        _producto = producto ?? throw new ArgumentNullException(nameof(producto));
        _http = new HttpClient(handler) { Timeout = tiempoEspera };
    }

    public void Dispose() => _http.Dispose();

    public async Task<ResultadoActivacionServidor> ActivarAsync(string clave, string hwid, CancellationToken cancellationToken = default)
    {
        using var respuesta = await LlamarAsync(
            "activate_product_license",
            new Dictionary<string, string> { ["p_key"] = clave, ["p_hwid"] = hwid, ["p_product"] = _producto },
            cancellationToken).ConfigureAwait(false);

        return TextoDe(respuesta.RootElement, "result") switch
        {
            "ok" => ResultadoActivacionServidor.Ok,
            "already_used" => ResultadoActivacionServidor.YaUsada,
            "revoked" => ResultadoActivacionServidor.Revocada,
            _ => ResultadoActivacionServidor.Invalida
        };
    }

    public async Task<long?> RegistrarPruebaAsync(string hwid, CancellationToken cancellationToken = default)
    {
        using var respuesta = await LlamarAsync(
            "register_product_trial",
            new Dictionary<string, string> { ["p_hwid"] = hwid, ["p_product"] = _producto },
            cancellationToken).ConfigureAwait(false);

        return TextoDe(respuesta.RootElement, "result") == "ok" ? EnteroDe(respuesta.RootElement, "started_at") : null;
    }

    public async Task<EstadoRemotoLicencia> ConsultarAsync(string hwid, CancellationToken cancellationToken = default)
    {
        using var respuesta = await LlamarAsync(
            "check_product_license",
            new Dictionary<string, string> { ["p_hwid"] = hwid, ["p_product"] = _producto },
            cancellationToken).ConfigureAwait(false);

        var clave = TextoDe(respuesta.RootElement, "status") switch
        {
            "activated" => EstadoClaveRemota.Activada,
            "revoked" => EstadoClaveRemota.Revocada,
            _ => EstadoClaveRemota.Ninguna
        };
        return new EstadoRemotoLicencia(clave, EnteroDe(respuesta.RootElement, "trial_started_at"));
    }

    private async Task<JsonDocument> LlamarAsync(string funcion, Dictionary<string, string> cuerpo, CancellationToken cancellationToken)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/rpc/{funcion}")
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
        };
        peticion.Headers.TryAddWithoutValidation("apikey", _claveAnon);
        // La anon key clásica es un JWT y va también como Bearer; la nueva "sb_publishable_..." no.
        if (_claveAnon.StartsWith("eyJ", StringComparison.Ordinal))
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _claveAnon);
        }

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(peticion, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Se agotó el tiempo de espera del HttpClient (no lo canceló quien llamó).
            throw new ServidorLicenciasException(MensajeSinConexion);
        }
        catch (HttpRequestException ex)
        {
            throw new ServidorLicenciasException(MensajeSinConexion, ex);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new ServidorLicenciasException($"El servidor de licencias respondió con un error ({(int)respuesta.StatusCode}).");
            }

            try
            {
                var texto = await respuesta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonDocument.Parse(texto);
            }
            catch (JsonException ex)
            {
                throw new ServidorLicenciasException("El servidor de licencias respondió con un formato inesperado.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ServidorLicenciasException(MensajeSinConexion, ex);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ServidorLicenciasException(MensajeSinConexion);
            }
        }
    }

    private static string? TextoDe(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static long? EnteroDe(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.Number
        && valor.TryGetInt64(out var numero)
            ? numero
            : null;
}
