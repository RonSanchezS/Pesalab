using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioPesajesTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private class UowFactoryQueFallaEnSegundoGuardar : IUnidadDeTrabajoFactory
    {
        private readonly IUnidadDeTrabajoFactory _inner;
        public UowFactoryQueFallaEnSegundoGuardar(IUnidadDeTrabajoFactory inner) => _inner = inner;

        public async Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default)
        {
            var uow = await _inner.CrearAsync(cancellationToken);
            return new UowDecorador(uow);
        }

        private class UowDecorador : IUnidadDeTrabajo
        {
            private readonly IUnidadDeTrabajo _inner;
            private int _llamadasGuardar;

            public UowDecorador(IUnidadDeTrabajo inner) => _inner = inner;

            public IUsuarioRepositorio Usuarios => _inner.Usuarios;
            public IAuditoriaRepositorio Auditoria => _inner.Auditoria;
            public IGanaderoRepositorio Ganaderos => _inner.Ganaderos;
            public IClienteRepositorio Clientes => _inner.Clientes;
            public IFleteroRepositorio Fleteros => _inner.Fleteros;
            public IParametrosRepositorio Parametros => _inner.Parametros;
            public IConfiguracionBalanzaRepositorio ConfiguracionBalanza => _inner.ConfiguracionBalanza;
            public IPesajeRepositorio Pesajes => _inner.Pesajes;

            public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
            {
                _llamadasGuardar++;
                if (_llamadasGuardar >= 2)
                {
                    throw new InvalidOperationException("Fallo simulado en segundo GuardarCambiosAsync");
                }
                return _inner.GuardarCambiosAsync(cancellationToken);
            }

            public Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default) =>
                _inner.IniciarTransaccionAsync(cancellationToken);

            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }

    private (ServicioPesajes Servicio, ISesionActual Sesion, ServicioBalanzaFalso Balanza, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        IUnidadDeTrabajoFactory? uowFactoryOverride = null,
        DateTimeOffset? tiempoInicial = null,
        ServicioBalanzaFalso? balanzaOverride = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4)));
        tiempo.SetLocalTimeZone(ZonaBolivia);
        var sesion = new SesionActual();
        var balanza = balanzaOverride ?? new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(
                1,
                EstadoBalanza.Conectada,
                120.0m,
                true,
                DateTimeOffset.UtcNow,
                null,
                "Conectada",
                false)
        };
        var factory = uowFactoryOverride ?? db.UnidadDeTrabajoFactory;
        var servicio = new ServicioPesajes(factory, sesion, balanza, new PesajeFaeneo.Tests.Utilidades.Licencias.ServicioLicenciaFalso(), tiempo, NullLogger<ServicioPesajes>.Instance);
        return (servicio, sesion, balanza, tiempo);
    }

    private static async Task<(int GanaderoId, int ClienteId)> SembrarGanaderoYClienteAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConCodigo("AH").ConNombre("GANADERO TEST").Build();
        var cliente = new ClienteBuilder().ConCodigo(100).ConNombre("CLIENTE TEST").Build();
        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        await ctx.SaveChangesAsync();
        return (ganadero.Id, cliente.Id);
    }

    private static RegistrarPesaje CrearDtoValido(int ganaderoId, int clienteId, int secuencia = 1, bool simulador = false)
    {
        return new RegistrarPesaje(
            GanaderoId: ganaderoId,
            ClienteId: clienteId,
            Direccion: "Calle Test 123",
            Secuencia: secuencia,
            ColorRaza: "Overo Colorado",
            Sexo: 'M',
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 120.0m,
            PesoDerecho: 118.5m,
            Observacion: "Observación de prueba",
            CapturadoConSimulador: simulador);
    }

    [Fact]
    public async Task Metodos_SinSesion_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _, _) = CrearServicio(db);
        var dto = CrearDtoValido(1, 1);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.SugerirSecuenciaAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.RegistrarAsync(dto));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarDelDiaAsync());
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public async Task Roles_OperadorSupervisorAdmin_PuedenRegistrar(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, rol);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        var resultado = await servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId));
        Assert.NotNull(resultado);
        Assert.True(resultado.Id > 0);
        Assert.True(resultado.NumeroBoleta > 0);
    }

    [Fact]
    public async Task RegistrarAsync_Valido_GrabaPesajeConDatosCorrectosYAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4));
        var (servicio, sesion, _, _) = CrearServicio(db, tiempoInicial: fechaEsperada);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        var dto = CrearDtoValido(ganaderoId, clienteId);
        var resultado = await servicio.RegistrarAsync(dto);

        Assert.Equal(1, resultado.NumeroBoleta);
        Assert.Equal(238.5m, resultado.PesoTotal);
        Assert.Equal(OrigenPeso.Balanza, resultado.Origen);
        Assert.Equal(fechaEsperada.UtcDateTime, resultado.FechaHoraUtc);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var pesaje = await ctx.Pesajes.FindAsync(resultado.Id);
        Assert.NotNull(pesaje);
        Assert.Equal(1, pesaje.NumeroBoleta);
        Assert.Equal(ganaderoId, pesaje.GanaderoId);
        Assert.Equal(clienteId, pesaje.ClienteId);
        Assert.Equal("CALLE TEST 123", pesaje.Direccion);
        Assert.Equal("OVERO COLORADO", pesaje.ColorRaza);
        Assert.Equal('M', pesaje.Sexo);
        Assert.Equal(TipoDesposte.ResCompleta, pesaje.TipoDesposte);
        Assert.Equal(120.0m, pesaje.PesoIzquierdo);
        Assert.Equal(118.5m, pesaje.PesoDerecho);
        Assert.Equal(238.5m, pesaje.PesoTotal);
        Assert.Equal(EstadoPesaje.Vigente, pesaje.Estado);
        Assert.Equal(usuario.Id, pesaje.UsuarioId);
        Assert.Equal(DateTimeKind.Utc, pesaje.FechaHora.Kind);

        // Parametros actualizados
        var parametros = await ctx.ParametrosSistema.FindAsync(1);
        Assert.NotNull(parametros);
        Assert.Equal(2, parametros.SiguienteNumeroBoleta);

        // Auditoría
        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.Entidad == EntidadesAuditoria.Pesaje && a.EntidadId == pesaje.Id.ToString());
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usuario.Id, auditoria.UsuarioId);
        Assert.Contains("Alta de pesaje boleta 1", auditoria.Motivo);
        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Null(auditoria.DatosAntesJson);
    }

    [Fact]
    public async Task RegistrarAsync_VeinteRegistrosSeguidos_NumerosConsecutivosSinHuecos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        var boletas = new List<long>();
        for (int i = 1; i <= 20; i++)
        {
            var res = await servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId, secuencia: i));
            boletas.Add(res.NumeroBoleta);
        }

        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), boletas);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var parametros = await ctx.ParametrosSistema.FindAsync(1);
        Assert.NotNull(parametros);
        Assert.Equal(21, parametros.SiguienteNumeroBoleta);
    }

    [Fact]
    public async Task RegistrarAsync_ContadorPreconfigurado_PrimeraBoletaUsaValorConfigurado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.ParametrosSistema.FindAsync(1);
            p!.SiguienteNumeroBoleta = 5000;
            await ctx.SaveChangesAsync();
        }

        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        var res = await servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId));
        Assert.Equal(5000, res.NumeroBoleta);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.ParametrosSistema.FindAsync(1);
            Assert.Equal(5001, p!.SiguienteNumeroBoleta);
        }
    }

    [Fact]
    public async Task RegistrarAsync_ErroresValidacion_NoConsumenNumero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        // GanaderoId <= 0
        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(0, clienteId)));
        Assert.Contains(ex1.Errores, e => e.Campo == CamposPesaje.Ganadero);

        // Secuencia <= 0
        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId, secuencia: 0)));
        Assert.Contains(ex2.Errores, e => e.Campo == CamposPesaje.Secuencia);

        // Pesos negativos
        var dtoPesosNegativos = new RegistrarPesaje(ganaderoId, clienteId, null, 1, null, 'M', TipoDesposte.ResCompleta, -5m, 100m, null, false);
        var ex3 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.RegistrarAsync(dtoPesosNegativos));
        Assert.Contains(ex3.Errores, e => e.Campo == CamposPesaje.Pesos);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var p = await ctx.ParametrosSistema.FindAsync(1);
        Assert.Equal(1, p!.SiguienteNumeroBoleta);
        Assert.Empty(await ctx.Pesajes.ToListAsync());
        Assert.Empty(await ctx.Auditorias.Where(a => a.Entidad == EntidadesAuditoria.Pesaje).ToListAsync());
    }

    [Fact]
    public async Task RegistrarAsync_GanaderoOClienteInactivo_LanzaValidacionExceptionYNoConsumeNumero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int ganaderoInactivoId;
        int clienteInactivoId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConCodigo("IN").Inactivo().Build();
            var c = new ClienteBuilder().ConCodigo(99).Inactivo().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();
            ganaderoInactivoId = g.Id;
            clienteInactivoId = c.Id;
        }

        var (ganaderoActivoId, clienteActivoId) = await SembrarGanaderoYClienteAsync(db);

        var exG = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoInactivoId, clienteActivoId)));
        Assert.Contains(exG.Errores, e => e.Campo == CamposPesaje.Ganadero);

        var exC = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoActivoId, clienteInactivoId)));
        Assert.Contains(exC.Errores, e => e.Campo == CamposPesaje.Cliente);

        await using var ctxFinal = await db.ContextFactory.CreateDbContextAsync();
        var parametros = await ctxFinal.ParametrosSistema.FindAsync(1);
        Assert.Equal(1, parametros!.SiguienteNumeroBoleta);
    }

    [Fact]
    public async Task RegistrarAsync_FalloEnAuditoria_HaceRollbackDePesajeYDeContador()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var uowFalla = new UowFactoryQueFallaEnSegundoGuardar(db.UnidadDeTrabajoFactory);
        var (servicio, sesion, _, _) = CrearServicio(db, uowFactoryOverride: uowFalla);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId)));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await ctx.Pesajes.ToListAsync());

        var parametros = await ctx.ParametrosSistema.FindAsync(1);
        Assert.Equal(1, parametros!.SiguienteNumeroBoleta);
    }

    [Fact]
    public async Task RegistrarAsync_NumeroBoletaDuplicado_LanzaReglaNegocioExceptionYNoAlteraContador()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var existente = new PesajeBuilder()
                .ConNumeroBoleta(7)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .Build();
            ctx.Pesajes.Add(existente);

            var parametros = await ctx.ParametrosSistema.FindAsync(1);
            parametros!.SiguienteNumeroBoleta = 7;
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId)));
        Assert.Equal("PES_NUMERO_DUPLICADO", ex.Codigo);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var parametros = await ctx.ParametrosSistema.FindAsync(1);
            Assert.Equal(7, parametros!.SiguienteNumeroBoleta);
        }
    }

    [Fact]
    public async Task RegistrarAsync_Simulador_OperadorYSupervisorRechazados_AdminPermitido()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var balanzaSimulada = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(
                1,
                EstadoBalanza.Conectada,
                120.0m,
                true,
                DateTimeOffset.UtcNow,
                null,
                "Simulador",
                true) // EsSimulador
        };

        var (servicio, sesion, _, _) = CrearServicio(db, balanzaOverride: balanzaSimulada);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        // Operador rechazado
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador, "op_sim");
        var exOp = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId)));
        Assert.Equal("PES_SIMULADOR", exOp.Codigo);

        // Supervisor rechazado
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor, "sup_sim");
        var exSup = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId)));
        Assert.Equal("PES_SIMULADOR", exSup.Codigo);

        // Administrador permitido
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador, "adm_sim");
        var resultado = await servicio.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId));
        Assert.Equal(OrigenPeso.Simulador, resultado.Origen);

        // Balanza real pero CapturadoConSimulador = true
        var balanzaReal = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(
                1,
                EstadoBalanza.Conectada,
                120.0m,
                true,
                DateTimeOffset.UtcNow,
                null,
                "Conectada",
                false) // Balanza real
        };
        var (servicioReal, sesionReal, _, _) = CrearServicio(db, balanzaOverride: balanzaReal);
        await SesionesDePrueba.IniciarAsync(db, sesionReal, Rol.Operador, "op_flag_sim");
        var exOpFlag = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicioReal.RegistrarAsync(CrearDtoValido(ganaderoId, clienteId, simulador: true)));
        Assert.Equal("PES_SIMULADOR", exOpFlag.Codigo);
    }

    [Fact]
    public async Task SugerirSecuenciaAsync_CalculaSecuenciaYCuentaVigentesDelDiaSegunZonaLocal()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        // 2026-09-27 02:30Z = 2026-09-26 22:30 en Bolivia (-04:00)
        var instantePrueba = new DateTimeOffset(2026, 9, 27, 2, 30, 0, TimeSpan.Zero);
        var (servicio, sesion, _, _) = CrearServicio(db, tiempoInicial: instantePrueba);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Pesaje 1: 26/09 12:00Z -> en Bolivia 26/09 08:00 (hoy). Secuencia 3, vigente.
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(100)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConSecuencia(3)
                .ConFechaHora(new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc))
                .Build());

            // Pesaje 2: 27/09 01:00Z -> en Bolivia 26/09 21:00 (hoy). Secuencia 4, vigente.
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(101)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConSecuencia(4)
                .ConFechaHora(new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc))
                .Build());

            // Pesaje 3: 26/09 03:00Z -> en Bolivia 25/09 23:00 (ayer). Secuencia 9, vigente. Fuera de hoy.
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(102)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConSecuencia(9)
                .ConFechaHora(new DateTime(2026, 9, 26, 3, 0, 0, DateTimeKind.Utc))
                .Build());

            // Pesaje 4: 26/09 15:00Z -> en Bolivia 26/09 11:00 (hoy). Secuencia 8, ANULADO.
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(103)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConSecuencia(8)
                .ConFechaHora(new DateTime(2026, 9, 26, 15, 0, 0, DateTimeKind.Utc))
                .Anulado("Anulado por prueba", usr.Id)
                .Build());

            await ctx.SaveChangesAsync();
        }

        var sugerencia = await servicio.SugerirSecuenciaAsync(ganaderoId);
        Assert.Equal(5, sugerencia.Siguiente); // max de vigentes hoy es 4 -> 4+1 = 5
        Assert.Equal(2, sugerencia.PesajesVigentesHoy); // solo sec 3 y sec 4

        // Ganadero sin pesajes
        var sugerenciaVacia = await servicio.SugerirSecuenciaAsync(999);
        Assert.Equal(1, sugerenciaVacia.Siguiente);
        Assert.Equal(0, sugerenciaVacia.PesajesVigentesHoy);
    }

    [Fact]
    public async Task ListarDelDiaAsync_DevuelveSoloDelDiaLocalEnOrdenDescendente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var hoyLocal = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(-4));
        var (servicio, sesion, _, _) = CrearServicio(db, tiempoInicial: hoyLocal);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (ganaderoId, clienteId) = await SembrarGanaderoYClienteAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Pesaje de ayer
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(50)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConFechaHora(hoyLocal.UtcDateTime.AddDays(-1))
                .Build());

            // Pesaje de hoy boleta 51
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(51)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConFechaHora(hoyLocal.UtcDateTime.AddHours(-2))
                .Build());

            // Pesaje de hoy boleta 52
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(52)
                .ConGanaderoId(ganaderoId)
                .ConClienteId(clienteId)
                .ConUsuarioId(usr.Id)
                .ConFechaHora(hoyLocal.UtcDateTime.AddHours(-1))
                .Build());

            await ctx.SaveChangesAsync();
        }

        var lista = await servicio.ListarDelDiaAsync();
        Assert.Equal(2, lista.Count);
        Assert.Equal(52, lista[0].NumeroBoleta);
        Assert.Equal(51, lista[1].NumeroBoleta);
    }
}
