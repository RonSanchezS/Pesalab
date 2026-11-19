using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioLicenciaTests
{
    private const string ClaveBuena = "0123-4567-89AB-CDEF-0123";
    private const long Dia = ReglasLicencia.SegundosDia;

    private static readonly DateTimeOffset Inicio = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class Entorno
    {
        public AlmacenLicenciaFalso Almacen { get; } = new();
        public ServidorLicenciasFalso Servidor { get; } = new();
        public IdentificadorEquipoFalso Equipo { get; } = new();
        public FakeTimeProvider Tiempo { get; } = new(Inicio);
        public ServicioLicencia Servicio { get; }
        public List<EstadoLicencia> Cambios { get; } = new();

        public Entorno()
        {
            Servicio = new ServicioLicencia(Almacen, Servidor, Equipo, Tiempo, NullLogger<ServicioLicencia>.Instance);
            Servicio.EstadoCambiado += (_, e) => Cambios.Add(e);
        }

        public long Ahora => Tiempo.GetUtcNow().ToUnixTimeSeconds();

        public string Hwid => Equipo.Hwid;
    }

    // ------------------------------------------------------------ estado, ExigirOperacion y CodigoEquipo

    [Fact]
    public void ObtenerEstado_SinDatos_EsNoIniciada_YNoPermiteOperar()
    {
        var e = new Entorno();

        Assert.Equal(EstadoLicencia.NoIniciada, e.Servicio.ObtenerEstado());
        Assert.False(e.Servicio.PermiteOperar);
    }

    [Fact]
    public void CodigoEquipo_SonLosOchoPrimerosCaracteresDelHwidEnMayusculasConGuion()
    {
        var e = new Entorno();
        e.Equipo.Hwid = "abcd1234ffffffffffffffff";
        var servicio = new ServicioLicencia(e.Almacen, e.Servidor, e.Equipo, e.Tiempo, NullLogger<ServicioLicencia>.Instance);

        Assert.Equal("ABCD-1234", servicio.CodigoEquipo);
    }

    [Fact]
    public void ExigirOperacion_ConPruebaVigente_NoLanza()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);

        e.Servicio.ExigirOperacion();
        Assert.True(e.Servicio.PermiteOperar);
    }

    [Fact]
    public void ExigirOperacion_Activada_NoLanza()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);

        e.Servicio.ExigirOperacion();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExigirOperacion_SinPruebaOVencida_LanzaModoConsultaConElMensajeDeD4(bool vencida)
    {
        var e = new Entorno();
        if (vencida)
        {
            e.Almacen.SembrarPrueba(e.Hwid, e.Ahora - 20 * Dia, e.Ahora - 20 * Dia);
        }

        var ex = Assert.Throws<ReglaNegocioException>(() => e.Servicio.ExigirOperacion());

        Assert.Equal("LIC_MODO_CONSULTA", ex.Codigo);
        Assert.Equal(
            "El sistema está en modo consulta: la prueba terminó o la licencia no está activa. Actívelo en Ayuda › Activar licencia.",
            ex.Message);
    }

    [Fact]
    public void ObtenerEstado_PruebaQueVenceConElPasoDelTiempo_PasaAModoConsulta_YNotificaElCambio()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);
        Assert.Equal(EstadoLicencia.EnPrueba(15), e.Servicio.ObtenerEstado());

        e.Tiempo.Advance(TimeSpan.FromDays(16));
        var estado = e.Servicio.ObtenerEstado();

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada), estado);
        var cambio = Assert.Single(e.Cambios);
        Assert.Equal(estado, cambio);
    }

    [Fact]
    public void ObtenerEstado_LaPrimeraLecturaNoCuentaComoCambio()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);

        e.Servicio.ObtenerEstado();
        e.Servicio.ObtenerEstado();

        Assert.Empty(e.Cambios);
    }

    // ------------------------------------------------------------ refresco de l

    [Fact]
    public void ObtenerEstado_RefrescaElUltimoVistoSoloCuandoAvanzoUnaHora()
    {
        var e = new Entorno();
        var inicio = e.Ahora;
        e.Almacen.SembrarPrueba(e.Hwid, inicio, inicio);

        e.Tiempo.Advance(TimeSpan.FromMinutes(30));
        e.Servicio.ObtenerEstado();
        Assert.Equal(0, e.Almacen.EscriturasPrueba);

        e.Tiempo.Advance(TimeSpan.FromMinutes(31));
        e.Servicio.ObtenerEstado();

        Assert.Equal(1, e.Almacen.EscriturasPrueba);
        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Ultimo);
        Assert.Equal(inicio.ToString(), e.Almacen.Datos.Inicio);
        Assert.Equal(FirmaLicencia.Prueba(inicio, e.Ahora, e.Hwid), e.Almacen.Datos.Firma);

        // La firma nueva sigue siendo válida: el estado no pasa a "alterada".
        Assert.Equal(TipoEstadoLicencia.Prueba, e.Servicio.ObtenerEstado().Tipo);
    }

    [Fact]
    public void ObtenerEstado_SiNoSePuedeEscribirElUltimoVisto_NoFalla()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);
        e.Almacen.ErrorAlEscribir = new UnauthorizedAccessException();
        e.Tiempo.Advance(TimeSpan.FromHours(3));

        Assert.Equal(TipoEstadoLicencia.Prueba, e.Servicio.ObtenerEstado().Tipo);
    }

    // ------------------------------------------------------------ ActivarAsync

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ActivarAsync_ClaveVacia_LanzaValidacion_SinLlamarAlServidor(string clave)
    {
        var e = new Entorno();

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => e.Servicio.ActivarAsync(clave));

        var error = Assert.Single(ex.Errores);
        Assert.Equal("Clave", error.Campo);
        Assert.Equal("Ingrese la clave de activación.", error.Mensaje);
        Assert.Equal(0, e.Servidor.ActivacionesRecibidas);
    }

    [Theory]
    [InlineData("0123-4567")]
    [InlineData("ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ")]
    [InlineData("0123-4567-89AB-CDEF-0123-4")]
    public async Task ActivarAsync_FormatoInvalido_LanzaValidacion_SinLlamarAlServidor(string clave)
    {
        var e = new Entorno();

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => e.Servicio.ActivarAsync(clave));

        Assert.Equal("La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.", Assert.Single(ex.Errores).Mensaje);
        Assert.Equal(0, e.Servidor.ActivacionesRecibidas);
    }

    [Fact]
    public async Task ActivarAsync_Ok_GuardaElTokenYDevuelveActivada_ConLaClaveNormalizada()
    {
        var e = new Entorno();
        e.Servidor.ResultadoActivacion = ResultadoActivacionServidor.Ok;

        var estado = await e.Servicio.ActivarAsync(" 0123 4567 89ab cdef 0123 ");

        Assert.Equal(EstadoLicencia.Activada, estado);
        Assert.Equal(ClaveBuena, Assert.Single(e.Servidor.Claves));
        Assert.Equal(FirmaLicencia.TokenActivacion(e.Hwid), e.Almacen.Datos.Activacion);
        Assert.Equal(EstadoLicencia.Activada, e.Servicio.ObtenerEstado());
    }

    [Fact]
    public async Task ActivarAsync_NotificaElCambioDeEstado()
    {
        var e = new Entorno();
        Assert.Equal(TipoEstadoLicencia.NoIniciada, e.Servicio.ObtenerEstado().Tipo);

        await e.Servicio.ActivarAsync(ClaveBuena);

        Assert.Equal(EstadoLicencia.Activada, Assert.Single(e.Cambios));
    }

    [Theory]
    [InlineData(ResultadoActivacionServidor.YaUsada, "LIC_CLAVE_USADA", "Esta clave ya fue utilizada en otro equipo.")]
    [InlineData(ResultadoActivacionServidor.Revocada, "LIC_CLAVE_REVOCADA", "Esta clave fue revocada.")]
    [InlineData(ResultadoActivacionServidor.Invalida, "LIC_CLAVE_INVALIDA", "Clave de activación inválida.")]
    public async Task ActivarAsync_RechazoDelServidor_LanzaReglaDeNegocio_YNoGuardaNada(
        ResultadoActivacionServidor resultado, string codigo, string mensaje)
    {
        var e = new Entorno();
        e.Servidor.ResultadoActivacion = resultado;

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.ActivarAsync(ClaveBuena));

        Assert.Equal(codigo, ex.Codigo);
        Assert.Equal(mensaje, ex.Message);
        Assert.Equal(0, e.Almacen.EscriturasActivacion);
        Assert.Null(e.Almacen.Datos.Activacion);
    }

    [Fact]
    public async Task ActivarAsync_SinConexion_PropagaElMensajeDelServidor_YNoGuardaNada()
    {
        var e = new Entorno();
        e.Servidor.Error = new ServidorLicenciasException("No se pudo contactar al servidor de licencias. Revise la conexión a internet.");

        var ex = await Assert.ThrowsAsync<ServidorLicenciasException>(() => e.Servicio.ActivarAsync(ClaveBuena));

        Assert.Equal("No se pudo contactar al servidor de licencias. Revise la conexión a internet.", ex.Message);
        Assert.Equal(0, e.Almacen.EscriturasActivacion);
    }

    [Fact]
    public async Task ActivarAsync_ElServidorAceptaPeroNoSePuedeEscribirLocalmente_LanzaLicAlmacen()
    {
        var e = new Entorno();
        e.Almacen.ErrorAlEscribir = new UnauthorizedAccessException();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.ActivarAsync(ClaveBuena));

        Assert.Equal("LIC_ALMACEN", ex.Codigo);
    }

    // ------------------------------------------------------------ IniciarPruebaAsync

    [Fact]
    public async Task IniciarPruebaAsync_ConConexion_RegistraEnElServidor_YEmpiezaCon15Dias()
    {
        var e = new Entorno();
        e.Servidor.InicioPruebaRegistrada = e.Ahora;

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(15), estado);
        Assert.Equal(1, e.Servidor.PruebasRegistradas);
        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Inicio);
        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Ultimo);
        Assert.Equal(FirmaLicencia.Prueba(e.Ahora, e.Ahora, e.Hwid), e.Almacen.Datos.Firma);
    }

    [Fact]
    public async Task IniciarPruebaAsync_SinConexion_IniciaLocalmente()
    {
        var e = new Entorno();
        e.Servidor.Error = new ServidorLicenciasException("sin red");

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(15), estado);
        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Inicio);
    }

    [Fact]
    public async Task IniciarPruebaAsync_ElServidorYaConoceElEquipoConUnaFechaAntigua_SeRespetaLaMasAntigua()
    {
        var e = new Entorno();
        e.Servidor.InicioPruebaRegistrada = e.Ahora - 5 * Dia;

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(10), estado);
        Assert.Equal((e.Ahora - 5 * Dia).ToString(), e.Almacen.Datos.Inicio);
    }

    [Fact]
    public async Task IniciarPruebaAsync_ElServidorYaConoceElEquipoConUnaPruebaVencida_NaceVencida()
    {
        var e = new Entorno();
        e.Servidor.InicioPruebaRegistrada = e.Ahora - 30 * Dia;

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada), estado);
    }

    [Fact]
    public async Task IniciarPruebaAsync_ElServidorDaUnaFechaFutura_UsaAhora()
    {
        var e = new Entorno();
        e.Servidor.InicioPruebaRegistrada = e.Ahora + 3 * Dia;

        await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Inicio);
    }

    [Fact]
    public async Task IniciarPruebaAsync_YaHayPrueba_NoReiniciaNiLlamaAlServidor()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora - 4 * Dia, e.Ahora - 4 * Dia);
        var antes = e.Almacen.Datos;

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(11), estado);
        Assert.Equal(0, e.Servidor.PruebasRegistradas);
        Assert.Equal(antes.Inicio, e.Almacen.Datos.Inicio);
    }

    [Fact]
    public async Task IniciarPruebaAsync_ConPruebaAlterada_NoLaReinicia()
    {
        var e = new Entorno();
        e.Almacen.Datos = new DatosLicenciaLocal("123", "456", "firma-falsa", null);

        var estado = await e.Servicio.IniciarPruebaAsync();

        Assert.Equal(EstadoLicencia.VencidaPor(MotivoVencimiento.Alterada), estado);
        Assert.Equal(0, e.Almacen.EscriturasPrueba);
    }

    [Fact]
    public async Task IniciarPruebaAsync_YaActivada_DevuelveActivada()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);

        Assert.Equal(EstadoLicencia.Activada, await e.Servicio.IniciarPruebaAsync());
        Assert.Equal(0, e.Servidor.PruebasRegistradas);
    }

    // ------------------------------------------------------------ SincronizarAsync (cinco casos de sync_blocking)

    [Fact]
    public async Task SincronizarAsync_SinConexion_DevuelveElEstadoLocal_SinLanzar()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);
        e.Servidor.Error = new ServidorLicenciasException("sin red");

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(15), estado);
        Assert.Equal(1, e.Servidor.Consultas);
    }

    [Fact]
    public async Task SincronizarAsync_ActivadaLocalmenteYRevocadaEnElServidor_BorraLaActivacion()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Revocada, null);

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.NoIniciada, estado);
        Assert.Equal(string.Empty, e.Almacen.Datos.Activacion);
        Assert.False(e.Servicio.PermiteOperar);
    }

    [Fact]
    public async Task SincronizarAsync_RevocadaConPruebaVigente_VuelveAPrueba()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora - 3 * Dia, e.Ahora - 3 * Dia);
        e.Almacen.SembrarActivacion(e.Hwid);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Revocada, e.Ahora - 3 * Dia);

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(12), estado);
    }

    [Fact]
    public async Task SincronizarAsync_ActivadaLocalmenteYEnElServidor_NoEscribeNada()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Activada, null);

        Assert.Equal(EstadoLicencia.Activada, await e.Servicio.SincronizarAsync());
        Assert.Equal(0, e.Almacen.EscriturasActivacion);
    }

    [Fact]
    public async Task SincronizarAsync_ElRegistroLocalSePerdioPeroElServidorLoTieneActivado_RestauraLaActivacion()
    {
        var e = new Entorno();
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Activada, null);

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.Activada, estado);
        Assert.Equal(FirmaLicencia.TokenActivacion(e.Hwid), e.Almacen.Datos.Activacion);
    }

    [Fact]
    public async Task SincronizarAsync_ElServidorTieneUnaPruebaMasAntigua_ReconciliaHaciaLaMasAntigua()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Ninguna, e.Ahora - 10 * Dia);

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.EnPrueba(5), estado);
        Assert.Equal((e.Ahora - 10 * Dia).ToString(), e.Almacen.Datos.Inicio);
        Assert.Equal(e.Ahora.ToString(), e.Almacen.Datos.Ultimo);
        Assert.Equal(0, e.Servidor.PruebasRegistradas);
    }

    [Fact]
    public async Task SincronizarAsync_ElServidorTieneUnaPruebaMasReciente_NoLaCambia()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora - 2 * Dia, e.Ahora - 2 * Dia);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Ninguna, e.Ahora);

        await e.Servicio.SincronizarAsync();

        // El último visto sí se refresca (pasaron más de una hora), pero el inicio de la prueba no cambia.
        Assert.Equal((e.Ahora - 2 * Dia).ToString(), e.Almacen.Datos.Inicio);
        Assert.Equal(0, e.Servidor.PruebasRegistradas);
    }

    [Fact]
    public async Task SincronizarAsync_ElServidorNoConoceLaPrueba_LaRegistra()
    {
        var e = new Entorno();
        e.Almacen.SembrarPrueba(e.Hwid, e.Ahora, e.Ahora);
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Ninguna, null);

        await e.Servicio.SincronizarAsync();

        Assert.Equal(1, e.Servidor.PruebasRegistradas);
    }

    [Fact]
    public async Task SincronizarAsync_SinPruebaNiActivacion_NoHaceNada()
    {
        var e = new Entorno();

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.NoIniciada, estado);
        Assert.Equal(0, e.Servidor.PruebasRegistradas);
        Assert.Equal(0, e.Almacen.EscriturasPrueba);
        Assert.Equal(0, e.Almacen.EscriturasActivacion);
    }

    [Fact]
    public async Task SincronizarAsync_SiElSuscriptorDelEventoFalla_NoSePropaga()
    {
        var e = new Entorno();
        e.Almacen.SembrarActivacion(e.Hwid);
        e.Servicio.ObtenerEstado();
        e.Servicio.EstadoCambiado += (_, _) => throw new InvalidOperationException("suscriptor roto");
        e.Servidor.Remoto = new EstadoRemotoLicencia(EstadoClaveRemota.Revocada, null);

        var estado = await e.Servicio.SincronizarAsync();

        Assert.Equal(EstadoLicencia.NoIniciada, estado);
    }
}
