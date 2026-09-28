using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioBalanzaTests
{
    private static ConfiguracionBalanzaDto CrearCandidataValida(TipoConexion tipo = TipoConexion.Tcp) => new(
        TipoConexion: tipo,
        PuertoCom: "COM1",
        BaudRate: 9600,
        DataBits: 8,
        Paridad: ParidadSerial.None,
        BitsParada: BitsParada.One,
        HostTcp: "127.0.0.1",
        PuertoTcp: 4001,
        PatronRegex: @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)",
        LecturasParaEstable: 5,
        ToleranciaKg: 0.5m,
        TimeoutMs: 3000);

    private class ProgresoPrueba : IProgress<AvancePruebaBalanza>
    {
        private readonly object _lock = new();
        private readonly List<AvancePruebaBalanza> _avances = new();

        public IReadOnlyList<AvancePruebaBalanza> Avances
        {
            get
            {
                lock (_lock) return _avances.ToList();
            }
        }

        public void Report(AvancePruebaBalanza value)
        {
            lock (_lock) _avances.Add(value);
        }
    }

    [Fact]
    public async Task Iniciar_PublicaConectandoYLuegoConectada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();

        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando creación de lector");
        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.Conectada, "esperando estado Conectada");

        Assert.Equal(EstadoBalanza.Conectada, servicio.Instantanea.Estado);
        Assert.Null(servicio.Instantanea.PesoKg);
        Assert.False(servicio.Instantanea.Estable);
    }

    [Fact]
    public async Task CincoTramasIguales_PublicaPesoEstable()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        for (int i = 0; i < 5; i++)
        {
            lector.Enviar("10.0");
        }

        await Esperar.HastaAsync(() => servicio.Instantanea.Estable, "esperando estabilidad");
        Assert.Equal(EstadoBalanza.Conectada, servicio.Instantanea.Estado);
        Assert.Equal(10.0m, servicio.Instantanea.PesoKg);
        Assert.True(servicio.Instantanea.Estable);
        Assert.NotNull(servicio.Instantanea.InstanteLectura);
    }

    [Fact]
    public async Task TramasQueVarianMasQueLaTolerancia_NoEsEstable()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        lector.Enviar("10.0");
        lector.Enviar("10.2");
        lector.Enviar("10.8"); // varía más de 0.5 kg
        lector.Enviar("10.3");
        lector.Enviar("10.1");

        await Esperar.HastaAsync(() => servicio.Instantanea.PesoKg == 10.1m, "esperando última lectura");
        Assert.False(servicio.Instantanea.Estable);
    }

    [Fact]
    public async Task TramasInvalidas_SoloTrasTresConsecutivas_PublicaTramasNoReconocidas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        // D6: 1ra y 2da trama inválida consecutivas NO deben disparar el estado (evita falsas alarmas).
        lector.Enviar("ABC");
        await Task.Delay(30);
        Assert.NotEqual(EstadoBalanza.TramasNoReconocidas, servicio.Instantanea.Estado);

        lector.Enviar("ABC");
        await Task.Delay(30);
        Assert.NotEqual(EstadoBalanza.TramasNoReconocidas, servicio.Instantanea.Estado);

        // 3ra trama inválida consecutiva: recién ahora se publica.
        lector.Enviar("ABC");
        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.TramasNoReconocidas, "esperando TramasNoReconocidas tras 3 tramas inválidas");
    }

    [Fact]
    public async Task TramaValidaEntreInvalidas_ReiniciaElContador_NoPublicaTramasNoReconocidas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        lector.Enviar("ABC");
        lector.Enviar("ABC");
        await Task.Delay(30);
        lector.Enviar("10.0"); // trama válida: reinicia el contador de inválidas
        await Esperar.HastaAsync(() => servicio.Instantanea.PesoKg == 10.0m, "esperando lectura válida");

        lector.Enviar("ABC");
        lector.Enviar("ABC");
        await Task.Delay(30);

        Assert.NotEqual(EstadoBalanza.TramasNoReconocidas, servicio.Instantanea.Estado);
    }

    [Fact]
    public async Task SinTramas_SoloTrasTresTimeoutsConsecutivos_PublicaSinDatosYAnulaElPeso()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        for (int i = 0; i < 5; i++)
        {
            lector.Enviar("15.0");
        }
        await Esperar.HastaAsync(() => servicio.Instantanea.Estable, "esperando estabilidad inicial");

        // D6: 1er y 2do vencimiento de TimeoutMs (3000 ms) consecutivos NO deben publicar SinDatos todavía.
        reloj.Advance(TimeSpan.FromMilliseconds(3000));
        await Task.Delay(30);
        Assert.NotEqual(EstadoBalanza.SinDatos, servicio.Instantanea.Estado);

        reloj.Advance(TimeSpan.FromMilliseconds(3000));
        await Task.Delay(30);
        Assert.NotEqual(EstadoBalanza.SinDatos, servicio.Instantanea.Estado);

        // 3er vencimiento consecutivo: recién ahora se publica SinDatos (y se anula el peso/estabilidad).
        reloj.Advance(TimeSpan.FromMilliseconds(3000));
        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.SinDatos, "esperando SinDatos tras 3 timeouts");
        Assert.Null(servicio.Instantanea.PesoKg);
        Assert.False(servicio.Instantanea.Estable);
    }

    [Fact]
    public async Task TresTimeoutsSeguidos_Reconecta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector 1");
        var primerLector = fabrica.Creados[0];

        // 3 vencimientos seguidos de TimeoutMs (3000 ms cada uno)
        await Esperar.AvanzandoHastaAsync(reloj, TimeSpan.FromMilliseconds(500), () => primerLector.Desechado, "esperando desecho de primer lector tras timeouts");
        Assert.True(primerLector.Desechado);

        // Espera de reconexión: 1 s
        await Esperar.AvanzandoHastaAsync(reloj, TimeSpan.FromMilliseconds(200), () => fabrica.Creados.Count == 2, "esperando lector 2 tras reconexión");
        Assert.Equal(2, fabrica.Creados.Count);
    }

    [Fact]
    public async Task FalloAlConectar_PublicaErrorConMensaje_YEsperaAntesDeReintentar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();

        var lectorFalla = new LectorBalanzaFalso { ExcepcionAlConectar = new ConexionBalanzaException("Fallo intencional") };
        fabrica.Encolar(lectorFalla);

        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);
        await servicio.IniciarAsync();

        await Esperar.HastaAsync(() => lectorFalla.Desechado, "esperando lector desechado");
        Assert.Equal("Fallo intencional", servicio.Instantanea.Mensaje);

        // Avanzar 900 ms -> sigue en intento 1 (espera es 1000 ms)
        reloj.Advance(TimeSpan.FromMilliseconds(900));
        await Task.Delay(20);
        Assert.Single(fabrica.Creados);

        // Avanzar 200 ms más (total > 1 s) -> crea el segundo lector
        reloj.Advance(TimeSpan.FromMilliseconds(200));
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 2, "esperando segundo lector");
        Assert.Equal(2, fabrica.Creados.Count);
    }

    [Fact]
    public async Task EsperasDeReintentoCrecen_1_2_4_YSeReiniciaTrasRecibirUnaTrama()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();

        // Encolar fallos sucesivos
        var f1 = new LectorBalanzaFalso { ExcepcionAlConectar = new ConexionBalanzaException("Error 1") };
        var f2 = new LectorBalanzaFalso { ExcepcionAlConectar = new ConexionBalanzaException("Error 2") };
        var f3 = new LectorBalanzaFalso { ExcepcionAlConectar = new ConexionBalanzaException("Error 3") };
        fabrica.Encolar(f1);
        fabrica.Encolar(f2);
        fabrica.Encolar(f3);

        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);
        await servicio.IniciarAsync();

        // Fallo 1 -> espera 1 s
        await Esperar.HastaAsync(() => f1.Desechado, "esperando intento 1 desechado");
        reloj.Advance(TimeSpan.FromSeconds(1));

        // Fallo 2 -> espera 2 s
        await Esperar.HastaAsync(() => f2.Desechado, "esperando intento 2 desechado");
        reloj.Advance(TimeSpan.FromSeconds(1.5));
        await Task.Delay(20);
        Assert.Equal(2, fabrica.Creados.Count); // Aún no pasaron 2 s
        reloj.Advance(TimeSpan.FromSeconds(0.6));

        // Fallo 3 -> espera 4 s
        await Esperar.HastaAsync(() => f3.Desechado, "esperando intento 3 desechado");
        reloj.Advance(TimeSpan.FromSeconds(3.5));
        await Task.Delay(20);
        Assert.Equal(3, fabrica.Creados.Count); // Aún no pasaron 4 s
        reloj.Advance(TimeSpan.FromSeconds(0.6));

        // Lector 4 conecta bien
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 4, "esperando intento 4");
        var lectorSano = fabrica.Creados[3];
        lectorSano.Enviar("50.0");
        await Esperar.HastaAsync(() => servicio.Instantanea.PesoKg == 50.0m, "esperando lectura sana");

        // Ahora fallar la lectura -> reintento debe volver a esperar 1 s (contador reiniciado)
        lectorSano.FallarLectura(new ConexionBalanzaException("Corte de red"));
        await Esperar.HastaAsync(() => lectorSano.Desechado, "esperando error tras lectura y lector desechado");

        reloj.Advance(TimeSpan.FromSeconds(1));
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 5, "esperando intento 5 con contador reiniciado a 1 s");
        Assert.Equal(5, fabrica.Creados.Count);
    }

    [Fact]
    public async Task ErrorDuranteLectura_PublicaError_YReconecta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        lector.FallarLectura(new ConexionBalanzaException("Error en socket"));
        await Esperar.HastaAsync(() => lector.Desechado, "esperando lector desechado");
        Assert.Equal("Error en socket", servicio.Instantanea.Mensaje);

        reloj.Advance(TimeSpan.FromSeconds(1));
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 2, "esperando reconexión");
    }

    [Fact]
    public async Task LectorQueLanzaIOException_NoRompeElBucle()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        lector.FallarLectura(new IOException("Fallo de hardware no traducido"));
        await Esperar.HastaAsync(() => lector.Desechado, "esperando lector desechado");
        Assert.Equal("Error de comunicación con la balanza.", servicio.Instantanea.Mensaje);

        reloj.Advance(TimeSpan.FromSeconds(1));
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 2, "esperando reconexión tras IOException");
    }

    [Fact]
    public async Task Detener_PublicaDetenida_DesechaElLector_YNoReconecta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        await servicio.DetenerAsync();
        Assert.Equal(EstadoBalanza.Detenida, servicio.Instantanea.Estado);
        Assert.True(lector.Desechado);

        reloj.Advance(TimeSpan.FromSeconds(60));
        await Task.Delay(20);
        Assert.Single(fabrica.Creados);
    }

    [Fact]
    public async Task IniciarDosVeces_UnSoloBucle()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await servicio.IniciarAsync();

        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        await Task.Delay(20);
        Assert.Single(fabrica.Creados);
    }

    [Fact]
    public async Task Reiniciar_RelecturaDeConfiguracion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector 1");

        // Cambiar PuertoCom en la base de datos
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            config.PuertoCom = "COM7";
            await ctx.SaveChangesAsync();
        }

        await servicio.ReiniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 2, "esperando lector 2 tras reiniciar");

        Assert.Equal("COM7", fabrica.ConfiguracionesRecibidas[^1].PuertoCom);
    }

    [Fact]
    public async Task PatronInvalidoEnBd_PublicaError_SinCrearLector()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Patrón sin grupo peso
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET PatronRegex = '\\d+' WHERE Id = 1;");
        }

        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.Error, "esperando Estado.Error");

        Assert.Empty(fabrica.Creados);
        Assert.Contains("Patrón de lectura inválido", servicio.Instantanea.Mensaje);
    }

    [Fact]
    public async Task Secuencia_EsEstrictamenteCreciente_Y_TramasRecientes_SeLimitanA50()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        long secAnterior = servicio.Instantanea.Secuencia;
        for (int i = 1; i <= 60; i++)
        {
            lector.Enviar($"peso_{i}\r\n");
            await Esperar.HastaAsync(() => servicio.Instantanea.Secuencia > secAnterior, $"esperando secuencia {i}");
            Assert.True(servicio.Instantanea.Secuencia > secAnterior);
            secAnterior = servicio.Instantanea.Secuencia;
        }

        var recientes = servicio.ObtenerTramasRecientes();
        Assert.Equal(50, recientes.Count);
        Assert.Contains("peso_60", recientes[^1]);
    }

    [Fact]
    public async Task ReiniciarAsync_SinPermiso_LanzaNoAutorizado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ReiniciarAsync());
    }

    [Fact]
    public async Task Reiniciar_ConConfiguracionSerial_EsperaMedioSegundoAntesDeReconectar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            config.TipoConexion = TipoConexion.Serial;
            config.PuertoCom = "COM5";
            await ctx.SaveChangesAsync();
        }

        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector 1");

        var tareaReiniciar = servicio.ReiniciarAsync();

        // D15: no debe reconectar antes de que transcurran 500 ms desde que se cerró el puerto serie.
        await Task.Delay(30);
        Assert.Single(fabrica.Creados);

        reloj.Advance(TimeSpan.FromMilliseconds(499));
        await Task.Delay(30);
        Assert.Single(fabrica.Creados);

        reloj.Advance(TimeSpan.FromMilliseconds(50));
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 2, "esperando lector 2 tras la pausa de 500 ms");

        await tareaReiniciar;
    }

    [Fact]
    public async Task ProbarAsync_SinPermiso_LanzaNoAutorizado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        var candidata = CrearCandidataValida();
        await Assert.ThrowsAsync<NoAutorizadoException>(() =>
            servicio.ProbarAsync(candidata, TimeSpan.FromSeconds(5), null));
    }

    [Fact]
    public async Task ProbarAsync_ConfiguracionInvalida_LanzaValidacion_SinPausarPrincipal()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector principal");
        var lectorPrincipal = fabrica.Creados[0];

        var candidataInvalida = CrearCandidataValida() with { PuertoTcp = 0 };
        await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ProbarAsync(candidataInvalida, TimeSpan.FromSeconds(5), null));

        Assert.False(lectorPrincipal.Desechado);
    }

    [Fact]
    public async Task ProbarAsync_PausaElPrincipal_UsaLaCandidata_Informa_YReanuda()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector principal");
        var lectorPrincipal = fabrica.Creados[0];

        var candidata = CrearCandidataValida() with { PuertoTcp = 5555 };
        var progreso = new ProgresoPrueba();

        var lectorPrueba = new LectorBalanzaFalso { Descripcion = "LectorPrueba" };
        fabrica.Encolar(lectorPrueba);

        var tareaPrueba = servicio.ProbarAsync(candidata, TimeSpan.FromSeconds(5), progreso);

        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.EnDiagnostico, "esperando EnDiagnostico");
        Assert.True(lectorPrincipal.Desechado);

        // Enviar tramas durante la prueba
        for (int i = 0; i < 5; i++)
        {
            lectorPrueba.Enviar("45.0");
        }

        await Esperar.HastaAsync(() => progreso.Avances.Count >= 5, "esperando avances en progreso");
        Assert.True(progreso.Avances[^1].Estable);

        // Vencer la duración de la prueba
        reloj.Advance(TimeSpan.FromSeconds(5));

        var resultado = await tareaPrueba;
        Assert.True(resultado.Conecto);
        Assert.False(resultado.Cancelada);
        Assert.Null(resultado.Error);
        Assert.Equal(5, resultado.TramasRecibidas);
        Assert.Equal(5, resultado.TramasValidas);
        Assert.Equal(45.0m, resultado.UltimoPesoKg);
        Assert.True(resultado.AlcanzoEstabilidad);

        // Se reanudó el lector con la configuración guardada de la BD
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 3, "esperando lector reanudado");
        Assert.Equal(TipoConexion.Simulador, fabrica.ConfiguracionesRecibidas[^1].TipoConexion);
    }

    [Fact]
    public async Task ProbarAsync_Cancelada_DevuelveCancelada_YReanuda()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector principal");

        var lectorPrueba = new LectorBalanzaFalso();
        fabrica.Encolar(lectorPrueba);

        using var cts = new CancellationTokenSource();
        var tareaPrueba = servicio.ProbarAsync(CrearCandidataValida(), TimeSpan.FromSeconds(10), null, cts.Token);

        await Esperar.HastaAsync(() => lectorPrueba.Conectado, "esperando conexión de prueba");
        cts.Cancel();

        var res = await tareaPrueba;
        Assert.True(res.Cancelada);

        await Esperar.HastaAsync(() => fabrica.Creados.Count == 3, "esperando reanudación tras cancelar");
    }

    [Fact]
    public async Task ProbarAsync_FalloDeConexion_DevuelveError_YReanuda()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector principal");

        var lectorPruebaFalla = new LectorBalanzaFalso
        {
            ExcepcionAlConectar = new ConexionBalanzaException("Error en IP de destino")
        };
        fabrica.Encolar(lectorPruebaFalla);

        var res = await servicio.ProbarAsync(CrearCandidataValida(), TimeSpan.FromSeconds(5), null);

        Assert.False(res.Conecto);
        Assert.False(res.Cancelada);
        Assert.Equal("Error en IP de destino", res.Error);

        await Esperar.HastaAsync(() => fabrica.Creados.Count == 3, "esperando reanudación tras fallo");
    }

    [Fact]
    public async Task ProbarAsync_CanceladaMientrasEsperaElTurno_DevuelveCancelada_SinLanzar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        var lectorPrimeraPrueba = new LectorBalanzaFalso();
        fabrica.Encolar(lectorPrimeraPrueba);

        // La primera prueba se queda "en curso" (conectada, sin tramas) reteniendo el turno interno.
        var primeraPrueba = servicio.ProbarAsync(CrearCandidataValida(), TimeSpan.FromSeconds(60), null);
        await Esperar.HastaAsync(() => lectorPrimeraPrueba.Conectado, "esperando conexión de la primera prueba");

        var lectorSegundaPrueba = new LectorBalanzaFalso();
        fabrica.Encolar(lectorSegundaPrueba);

        using var ctsSegunda = new CancellationTokenSource();
        var segundaPrueba = servicio.ProbarAsync(CrearCandidataValida(), TimeSpan.FromSeconds(60), null, ctsSegunda.Token);

        // La segunda prueba está en cola esperando el semáforo interno; se cancela antes de tomar el turno.
        ctsSegunda.Cancel();
        var resultadoSegunda = await segundaPrueba;

        Assert.True(resultadoSegunda.Cancelada);
        Assert.False(resultadoSegunda.Conecto);

        reloj.Advance(TimeSpan.FromSeconds(60));
        var resultadoPrimera = await primeraPrueba;
        Assert.True(resultadoPrimera.Conecto);
    }

    [Fact]
    public async Task ProbarAsync_ServicioNoIniciado_NoLoIniciaAlTerminar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        var tareaPrueba = servicio.ProbarAsync(CrearCandidataValida(), TimeSpan.FromSeconds(5), null);
        reloj.Advance(TimeSpan.FromSeconds(5));
        var res = await tareaPrueba;

        Assert.True(res.Conecto);
        Assert.Equal(EstadoBalanza.Detenida, servicio.Instantanea.Estado);
    }

    [Fact]
    public async Task Dispose_DetieneElBucle()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");
        var lector = fabrica.Creados[0];

        servicio.Dispose();
        await Esperar.HastaAsync(() => lector.Desechado, "esperando desecho por dispose");
        Assert.True(lector.Desechado);
    }

    [Fact]
    public async Task TrasDispose_OperacionesPublicas_LanzanObjectDisposed()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var reloj = new FakeTimeProvider();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var fabrica = new FabricaLectorFalsa();
        var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => fabrica.Creados.Count == 1, "esperando lector");

        await servicio.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => servicio.IniciarAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => servicio.DetenerAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => servicio.ReiniciarAsync());
    }

    [Theory]
    [InlineData(EstadoBalanza.Conectada, true, 100.0, 0, 5, true)]
    [InlineData(EstadoBalanza.Conectada, false, 100.0, 0, 5, false)]
    [InlineData(EstadoBalanza.Detenida, true, 100.0, 0, 5, false)]
    [InlineData(EstadoBalanza.SinDatos, true, null, 0, 5, false)]
    [InlineData(EstadoBalanza.Conectada, true, 100.0, 6, 5, false)] // lectura vieja por 1 s más que antigüedad máxima
    public void EsCapturable_CasosPuros_RetornaEsperado(
        EstadoBalanza estado,
        bool estable,
        object? peso,
        int segundosAntiguedad,
        int segundosMaximos,
        bool esperado)
    {
        var ahora = new DateTimeOffset(2026, 9, 25, 10, 0, 10, TimeSpan.Zero);
        var instante = ahora.AddSeconds(-segundosAntiguedad);
        decimal? pesoKg = peso is null ? null : Convert.ToDecimal(peso);

        var instantanea = new InstantaneaBalanza(
            Secuencia: 1,
            Estado: estado,
            PesoKg: pesoKg,
            Estable: estable,
            InstanteLectura: instante,
            Mensaje: null,
            Descripcion: "Test",
            EsSimulador: false);

        bool capturable = instantanea.EsCapturable(ahora, TimeSpan.FromSeconds(segundosMaximos));
        Assert.Equal(esperado, capturable);
    }

    [Fact]
    public void EsCapturable_SobrecargaConTimeoutMs_RespetaLimiteExacto()
    {
        var ahora = new DateTimeOffset(2026, 9, 25, 10, 0, 10, TimeSpan.Zero);
        var instantanea = new InstantaneaBalanza(
            Secuencia: 1,
            Estado: EstadoBalanza.Conectada,
            PesoKg: 100.0m,
            Estable: true,
            InstanteLectura: ahora.AddMilliseconds(-2500),
            Mensaje: null,
            Descripcion: "Test",
            EsSimulador: false)
        {
            TimeoutMs = 2500
        };

        Assert.True(instantanea.EsCapturable(ahora));

        var instantaneaVieja = instantanea with { InstanteLectura = ahora.AddMilliseconds(-2501) };
        Assert.False(instantaneaVieja.EsCapturable(ahora));
    }

    [Fact]
    public async Task Iniciar_PublicaInstantaneaConTimeoutMsDeConfiguracion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET TimeoutMs = 2500 WHERE Id = 1;");
        }

        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        var fabrica = new FabricaLectorFalsa();
        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, reloj, NullLogger<ServicioBalanza>.Instance);

        await servicio.IniciarAsync();
        await Esperar.HastaAsync(() => servicio.Instantanea.Estado == EstadoBalanza.Conectada, "esperando conexión");

        Assert.Equal(2500, servicio.Instantanea.TimeoutMs);
    }
}
