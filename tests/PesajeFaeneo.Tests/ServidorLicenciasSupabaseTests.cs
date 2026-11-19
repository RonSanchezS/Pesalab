using System.Net;
using System.Text;
using System.Text.Json;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Infrastructure.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServidorLicenciasSupabaseTests
{
    private const string Url = "https://ejemplo.supabase.co/";
    private const string Anon = "eyJ.anon.clave";
    private const string Hwid = "e704a377c9e29b967b41c7c8c793d1d0e1c9ee287d0082abab837cdb463dc5aa";

    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respuesta;

        public ManejadorFalso(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respuesta) => _respuesta = respuesta;

        public HttpRequestMessage? Peticion { get; private set; }

        public string? Cuerpo { get; private set; }

        public int Llamadas { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Llamadas++;
            Peticion = request;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respuesta(request, cancellationToken);
        }
    }

    private static ManejadorFalso Responde(string json, HttpStatusCode codigo = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    private static ServidorLicenciasSupabase Crear(HttpMessageHandler manejador, TimeSpan? espera = null, string anon = Anon) =>
        new(manejador, Url, anon, "pesajefaeneo", espera ?? TimeSpan.FromSeconds(8));

    private static JsonElement CuerpoJson(ManejadorFalso m) => JsonDocument.Parse(m.Cuerpo!).RootElement;

    // ------------------------------------------------------------ constantes

    [Fact]
    public void Constantes_SonLasDelPlan()
    {
        Assert.Equal("https://uxzehuncusnaofmfjydk.supabase.co", ConstantesLicencia.UrlServidor);
        Assert.Equal("pesajefaeneo", ConstantesLicencia.Producto);
        Assert.StartsWith("eyJ", ConstantesLicencia.ClaveAnon);
        Assert.Equal(@"Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg", ConstantesLicencia.SubclaveRegistro);
        Assert.Equal(8, ConstantesLicencia.TiempoEsperaSegundos);
    }

    // ------------------------------------------------------------ activar

    [Fact]
    public async Task ActivarAsync_EnviaLaPeticionRpcConCabecerasYCuerpoDelProducto()
    {
        var m = Responde("{\"result\":\"ok\"}");
        using var servidor = Crear(m);

        var r = await servidor.ActivarAsync("0123-4567-89AB-CDEF-0123", Hwid);

        Assert.Equal(ResultadoActivacionServidor.Ok, r);
        Assert.Equal(HttpMethod.Post, m.Peticion!.Method);
        Assert.Equal("https://ejemplo.supabase.co/rest/v1/rpc/activate_product_license", m.Peticion.RequestUri!.ToString());
        Assert.Equal(Anon, Assert.Single(m.Peticion.Headers.GetValues("apikey")));
        Assert.Equal("Bearer", m.Peticion.Headers.Authorization!.Scheme);
        Assert.Equal(Anon, m.Peticion.Headers.Authorization.Parameter);
        Assert.Equal("application/json", m.Peticion.Content!.Headers.ContentType!.MediaType);

        var cuerpo = CuerpoJson(m);
        Assert.Equal("0123-4567-89AB-CDEF-0123", cuerpo.GetProperty("p_key").GetString());
        Assert.Equal(Hwid, cuerpo.GetProperty("p_hwid").GetString());
        Assert.Equal("pesajefaeneo", cuerpo.GetProperty("p_product").GetString());
    }

    [Fact]
    public async Task ActivarAsync_ConLaClaveNuevaSbPublishable_NoEnviaBearer()
    {
        var m = Responde("{\"result\":\"ok\"}");
        using var servidor = Crear(m, anon: "sb_publishable_abc");

        await servidor.ActivarAsync("0123-4567-89AB-CDEF-0123", Hwid);

        Assert.Equal("sb_publishable_abc", Assert.Single(m.Peticion!.Headers.GetValues("apikey")));
        Assert.Null(m.Peticion.Headers.Authorization);
    }

    [Theory]
    [InlineData("{\"result\":\"ok\"}", ResultadoActivacionServidor.Ok)]
    [InlineData("{\"result\":\"invalid\"}", ResultadoActivacionServidor.Invalida)]
    [InlineData("{\"result\":\"revoked\"}", ResultadoActivacionServidor.Revocada)]
    [InlineData("{\"result\":\"already_used\"}", ResultadoActivacionServidor.YaUsada)]
    [InlineData("{\"result\":\"otra-cosa\"}", ResultadoActivacionServidor.Invalida)]
    [InlineData("{}", ResultadoActivacionServidor.Invalida)]
    [InlineData("[]", ResultadoActivacionServidor.Invalida)]
    public async Task ActivarAsync_TraduceElResultado(string json, ResultadoActivacionServidor esperado)
    {
        using var servidor = Crear(Responde(json));

        Assert.Equal(esperado, await servidor.ActivarAsync("0123-4567-89AB-CDEF-0123", Hwid));
    }

    // ------------------------------------------------------------ registrar prueba

    [Fact]
    public async Task RegistrarPruebaAsync_UsaLaFuncionDelProducto_YDevuelveStartedAt()
    {
        var m = Responde("{\"result\":\"ok\",\"started_at\":1760356800}");
        using var servidor = Crear(m);

        var inicio = await servidor.RegistrarPruebaAsync(Hwid);

        Assert.Equal(1760356800L, inicio);
        Assert.EndsWith("/rest/v1/rpc/register_product_trial", m.Peticion!.RequestUri!.ToString());
        var cuerpo = CuerpoJson(m);
        Assert.Equal(Hwid, cuerpo.GetProperty("p_hwid").GetString());
        Assert.Equal("pesajefaeneo", cuerpo.GetProperty("p_product").GetString());
        Assert.False(cuerpo.TryGetProperty("p_key", out _));
    }

    [Theory]
    [InlineData("{\"result\":\"invalid\"}")]
    [InlineData("{\"result\":\"ok\"}")]
    [InlineData("{\"result\":\"ok\",\"started_at\":null}")]
    public async Task RegistrarPruebaAsync_SinFechaValida_DevuelveNull(string json)
    {
        using var servidor = Crear(Responde(json));

        Assert.Null(await servidor.RegistrarPruebaAsync(Hwid));
    }

    // ------------------------------------------------------------ consultar

    [Theory]
    [InlineData("{\"status\":\"activated\",\"trial_started_at\":1760356800}", EstadoClaveRemota.Activada, 1760356800L)]
    [InlineData("{\"status\":\"revoked\",\"trial_started_at\":null}", EstadoClaveRemota.Revocada, null)]
    [InlineData("{\"status\":\"none\",\"trial_started_at\":1700000000}", EstadoClaveRemota.Ninguna, 1700000000L)]
    [InlineData("{\"status\":\"none\"}", EstadoClaveRemota.Ninguna, null)]
    [InlineData("{}", EstadoClaveRemota.Ninguna, null)]
    public async Task ConsultarAsync_TraduceElEstado(string json, EstadoClaveRemota clave, long? inicioPrueba)
    {
        var m = Responde(json);
        using var servidor = Crear(m);

        var r = await servidor.ConsultarAsync(Hwid);

        Assert.Equal(clave, r.Clave);
        Assert.Equal(inicioPrueba, r.InicioPrueba);
        Assert.EndsWith("/rest/v1/rpc/check_product_license", m.Peticion!.RequestUri!.ToString());
        Assert.Equal("pesajefaeneo", CuerpoJson(m).GetProperty("p_product").GetString());
    }

    // ------------------------------------------------------------ errores

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "El servidor de licencias respondió con un error (401).")]
    [InlineData(HttpStatusCode.InternalServerError, "El servidor de licencias respondió con un error (500).")]
    [InlineData(HttpStatusCode.NotFound, "El servidor de licencias respondió con un error (404).")]
    public async Task RespuestaNoExitosa_LanzaServidorLicenciasExceptionConElCodigo(HttpStatusCode codigo, string mensaje)
    {
        using var servidor = Crear(Responde("{\"message\":\"x\"}", codigo));

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => servidor.ConsultarAsync(Hwid));

        Assert.Equal(mensaje, ex.Message);
    }

    [Fact]
    public async Task ErrorDeRed_LanzaServidorLicenciasExceptionConElMensajeDeD9()
    {
        var m = new ManejadorFalso((_, _) => throw new HttpRequestException("sin red"));
        using var servidor = Crear(m);

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => servidor.ActivarAsync("0123-4567-89AB-CDEF-0123", Hwid));

        Assert.Equal("No se pudo contactar al servidor de licencias. Revise la conexión a internet.", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task TiempoAgotado_LanzaServidorLicenciasException()
    {
        var m = new ManejadorFalso(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var servidor = Crear(m, TimeSpan.FromMilliseconds(100));

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => servidor.RegistrarPruebaAsync(Hwid));

        Assert.Equal("No se pudo contactar al servidor de licencias. Revise la conexión a internet.", ex.Message);
    }

    [Fact]
    public async Task CancelacionDelLlamador_NoSeConvierteEnErrorDeServidor()
    {
        var m = new ManejadorFalso(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var servidor = Crear(m);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servidor.RegistrarPruebaAsync(Hwid, cts.Token));
    }

    [Fact]
    public async Task RespuestaQueNoEsJson_LanzaServidorLicenciasException()
    {
        using var servidor = Crear(Responde("<html>no es json</html>"));

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => servidor.ConsultarAsync(Hwid));

        Assert.Equal("El servidor de licencias respondió con un formato inesperado.", ex.Message);
    }

    [Fact]
    public async Task ElMensajeDeErrorNuncaContieneLaClave()
    {
        var m = new ManejadorFalso((_, _) => throw new HttpRequestException("falló https://x/?p_key=0123-4567-89AB-CDEF-0123"));
        using var servidor = Crear(m);

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => servidor.ActivarAsync("0123-4567-89AB-CDEF-0123", Hwid));

        Assert.DoesNotContain("0123-4567", ex.Message);
    }
}
