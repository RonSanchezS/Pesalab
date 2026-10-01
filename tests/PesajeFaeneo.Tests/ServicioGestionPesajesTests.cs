using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Tests;

file static class ExtensionesPesajePrueba
{
    public static Pesaje ConDetalles(
        this Pesaje p,
        string? direccion = null,
        string? colorRaza = null,
        char? sexo = null,
        string? observacion = null)
    {
        if (direccion != null) p.Direccion = direccion;
        if (colorRaza != null) p.ColorRaza = colorRaza;
        if (sexo.HasValue) p.Sexo = sexo.Value;
        if (observacion != null) p.Observacion = observacion;
        return p;
    }
}

public class ServicioGestionPesajesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Gestion_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private class UowFactoryContadorGuardar : IUnidadDeTrabajoFactory
    {
        private readonly IUnidadDeTrabajoFactory _inner;
        public int LlamadasGuardar { get; private set; }
        public bool DebeFallar { get; set; }

        public UowFactoryContadorGuardar(IUnidadDeTrabajoFactory inner) => _inner = inner;

        public async Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default)
        {
            var uow = await _inner.CrearAsync(cancellationToken);
            return new UowDecorador(this, uow);
        }

        private class UowDecorador : IUnidadDeTrabajo
        {
            private readonly UowFactoryContadorGuardar _factory;
            private readonly IUnidadDeTrabajo _inner;

            public UowDecorador(UowFactoryContadorGuardar factory, IUnidadDeTrabajo inner)
            {
                _factory = factory;
                _inner = inner;
            }

            public IUsuarioRepositorio Usuarios => _inner.Usuarios;
            public IAuditoriaRepositorio Auditoria => _inner.Auditoria;
            public IGanaderoRepositorio Ganaderos => _inner.Ganaderos;
            public IClienteRepositorio Clientes => _inner.Clientes;
            public IFleteroRepositorio Fleteros => _inner.Fleteros;
            public IParametrosRepositorio Parametros => _inner.Parametros;
            public IConfiguracionBalanzaRepositorio ConfiguracionBalanza => _inner.ConfiguracionBalanza;
            public IPesajeRepositorio Pesajes => _inner.Pesajes;

            public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
            {
                _factory.LlamadasGuardar++;
                if (_factory.DebeFallar)
                {
                    throw new InvalidOperationException("Fallo simulado en GuardarCambiosAsync");
                }
                await _inner.GuardarCambiosAsync(cancellationToken);
            }

            public Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default) =>
                _inner.IniciarTransaccionAsync(cancellationToken);

            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }

    private static (ServicioGestionPesajes Servicio, SesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        IUnidadDeTrabajoFactory? uowFactoryOverride = null,
        DateTimeOffset? tiempoInicial = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(ZonaBolivia);
        var sesion = new SesionActual();
        var factory = uowFactoryOverride ?? db.UnidadDeTrabajoFactory;
        var servicio = new ServicioGestionPesajes(factory, sesion, tiempo, NullLogger<ServicioGestionPesajes>.Instance);
        return (servicio, sesion, tiempo);
    }

    private static async Task<(int GanaderoId, int ClienteId)> SembrarGanaderoYClienteAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConMarca("AH").ConNombre("GANADERO TEST").Build();
        var cliente = new ClienteBuilder().ConCodigo(100).ConNombre("CLIENTE TEST").Build();
        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        await ctx.SaveChangesAsync();
        return (ganadero.Id, cliente.Id);
    }

    // =========================================================================
    // BÚSQUEDA
    // =========================================================================

    [Fact]
    public async Task BuscarAsync_SinSesion_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() =>
            servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, null)));
    }

    [Fact]
    public async Task BuscarAsync_RolOperador_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        await Assert.ThrowsAsync<NoAutorizadoException>(() =>
            servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, null)));
    }

    [Theory]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public async Task BuscarAsync_SupervisorYAdmin_PuedenBuscar(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, rol);

        var res = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, null));
        Assert.NotNull(res);
        Assert.Empty(res.Boletas);
    }

    [Fact]
    public async Task BuscarAsync_FechaDelDiaLocal_CruceMedianocheUtcManejaRangoCorrectamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        // 2026-09-28 02:30:00 UTC = 2026-09-27 22:30:00 Local (UTC-4)
        var fechaUtc = new DateTime(2026, 9, 28, 2, 30, 0, DateTimeKind.Utc);
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(1234)
                .ConFechaHora(fechaUtc)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .Build());
            await ctx.SaveChangesAsync();
        }

        // Buscar el 27 local: debe aparecer
        var filtro27 = new FiltroBoletas(
            Desde: new DateOnly(2026, 9, 27),
            Hasta: new DateOnly(2026, 9, 27),
            NumeroBoleta: null,
            GanaderoId: null,
            ClienteId: null,
            Estado: null);
        var res27 = await servicio.BuscarAsync(filtro27);
        var boleta = Assert.Single(res27.Boletas);
        Assert.Equal(1234, boleta.NumeroBoleta);

        // Buscar el 28 local: NO debe aparecer
        var filtro28 = new FiltroBoletas(
            Desde: new DateOnly(2026, 9, 28),
            Hasta: new DateOnly(2026, 9, 28),
            NumeroBoleta: null,
            GanaderoId: null,
            ClienteId: null,
            Estado: null);
        var res28 = await servicio.BuscarAsync(filtro28);
        Assert.Empty(res28.Boletas);
    }

    [Fact]
    public async Task BuscarAsync_FechaDesdeMayorQueHasta_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroBoletas(
            Desde: new DateOnly(2026, 9, 28),
            Hasta: new DateOnly(2026, 9, 27),
            NumeroBoleta: null,
            GanaderoId: null,
            ClienteId: null,
            Estado: null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.BuscarAsync(filtro));
        Assert.Contains(ex.Errores, e => e.Campo == CamposGestionPesajes.FechaHasta);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BuscarAsync_NumeroBoletaMenorOIgualACero_LanzaValidacionException(long numeroInvalido)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroBoletas(
            Desde: null,
            Hasta: null,
            NumeroBoleta: numeroInvalido,
            GanaderoId: null,
            ClienteId: null,
            Estado: null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.BuscarAsync(filtro));
        Assert.Contains(ex.Errores, e => e.Campo == CamposGestionPesajes.NumeroBoleta);
    }

    [Fact]
    public async Task BuscarAsync_FiltrosPorNumeroGanaderoClienteEstado_YCombinacionAnd()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, c1) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int g2, c2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var gan2 = new GanaderoBuilder().ConMarca("B2").ConNombre("GAN 2").Build();
            var cli2 = new ClienteBuilder().ConCodigo(200).ConNombre("CLI 2").Build();
            ctx.Ganaderos.Add(gan2);
            ctx.Clientes.Add(cli2);
            await ctx.SaveChangesAsync();
            g2 = gan2.Id;
            c2 = cli2.Id;

            ctx.Pesajes.AddRange(
                new PesajeBuilder().ConNumeroBoleta(100).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u.Id).Build(),
                new PesajeBuilder().ConNumeroBoleta(200).ConGanaderoId(g2).ConClienteId(c1).ConUsuarioId(u.Id).Build(),
                new PesajeBuilder().ConNumeroBoleta(300).ConGanaderoId(g1).ConClienteId(c2).ConUsuarioId(u.Id).Build(),
                new PesajeBuilder().ConNumeroBoleta(400).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u.Id).Anulado("MOTIVO ANULACION", u.Id).Build()
            );
            await ctx.SaveChangesAsync();
        }

        // Por número exacto
        var rNum = await servicio.BuscarAsync(new FiltroBoletas(null, null, 200, null, null, null));
        Assert.Single(rNum.Boletas);
        Assert.Equal(200, rNum.Boletas[0].NumeroBoleta);

        // Por ganadero
        var rGan = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, g2, null, null));
        Assert.Single(rGan.Boletas);
        Assert.Equal(200, rGan.Boletas[0].NumeroBoleta);

        // Por cliente
        var rCli = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, c2, null));
        Assert.Single(rCli.Boletas);
        Assert.Equal(300, rCli.Boletas[0].NumeroBoleta);

        // Por estado
        var rEst = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, EstadoPesaje.Anulado));
        Assert.Single(rEst.Boletas);
        Assert.Equal(400, rEst.Boletas[0].NumeroBoleta);

        // Combinación AND: Ganadero g1 Y Estado Vigente
        var rAnd = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, g1, null, EstadoPesaje.Vigente));
        Assert.Equal(2, rAnd.Boletas.Count);
        Assert.Contains(rAnd.Boletas, b => b.NumeroBoleta == 100);
        Assert.Contains(rAnd.Boletas, b => b.NumeroBoleta == 300);
    }

    [Fact]
    public async Task BuscarAsync_Truncado_Con201PesajesRetorna200YTruncadoTrue_Con200Retorna200YTruncadoFalse()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        // Insertar 200 pesajes
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var lote = new List<Pesaje>(200);
            for (int i = 1; i <= 200; i++)
            {
                lote.Add(new PesajeBuilder().ConNumeroBoleta(1000 + i).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build());
            }
            ctx.Pesajes.AddRange(lote);
            await ctx.SaveChangesAsync();
        }

        var res200 = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, null));
        Assert.Equal(200, res200.Boletas.Count);
        Assert.False(res200.Truncado);

        // Insertar el 201
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1201).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build());
            await ctx.SaveChangesAsync();
        }

        var res201 = await servicio.BuscarAsync(new FiltroBoletas(null, null, null, null, null, null));
        Assert.Equal(200, res201.Boletas.Count);
        Assert.True(res201.Truncado);
    }

    [Fact]
    public async Task BuscarAsync_MapeaFechaHoraALocalKindUnspecified()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        // 14:00 UTC = 10:00 Local (UTC-4)
        var utc = new DateTime(2026, 9, 28, 14, 0, 0, DateTimeKind.Utc);
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(777).ConFechaHora(utc).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build());
            await ctx.SaveChangesAsync();
        }

        var res = await servicio.BuscarAsync(new FiltroBoletas(null, null, 777, null, null, null));
        var b = Assert.Single(res.Boletas);
        Assert.Equal(DateTimeKind.Unspecified, b.FechaHoraLocal.Kind);
        Assert.Equal(10, b.FechaHoraLocal.Hour);
    }

    [Fact]
    public async Task ObtenerOpcionesBusquedaAsync_IncluyeGanaderosYClientesInactivos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Ganaderos.AddRange(
                new GanaderoBuilder().ConMarca("ACT").ConNombre("GAN ACT").Build(),
                new GanaderoBuilder().ConMarca("INA").ConNombre("GAN INA").Inactivo().Build()
            );
            ctx.Clientes.AddRange(
                new ClienteBuilder().ConCodigo(10).ConNombre("CLI ACT").Build(),
                new ClienteBuilder().ConCodigo(20).ConNombre("CLI INA").Inactivo().Build()
            );
            await ctx.SaveChangesAsync();
        }

        var opciones = await servicio.ObtenerOpcionesBusquedaAsync();
        Assert.Equal(2, opciones.Ganaderos.Count);
        Assert.Contains(opciones.Ganaderos, g => g.Codigo == "ACT" && g.Activo);
        Assert.Contains(opciones.Ganaderos, g => g.Codigo == "INA" && !g.Activo);

        Assert.Equal(2, opciones.Clientes.Count);
        Assert.Contains(opciones.Clientes, c => c.Codigo == "10" && c.Activo);
        Assert.Contains(opciones.Clientes, c => c.Codigo == "20" && !c.Activo);
    }

    // =========================================================================
    // EDICIÓN
    // =========================================================================

    [Fact]
    public async Task EditarAsync_RolOperador_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var dto = new EditarPesaje(1, 0, 1, 1, "DIR", 1, "B", 'M', "OBS", null);
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(dto));
    }

    [Fact]
    public async Task EditarAsync_CambiarDireccionYObservacion_PersisteNormalizadoIncrementaVersionYRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        DateTime fechaOriginalUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(8001)
                .ConFechaHora(fechaOriginalUtc)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .ConPesos(110.0m, 115.0m)
                .ConDesposte(TipoDesposte.ResCompleta)
                .ConOrigen(OrigenPeso.Balanza)
                .Build()
                .ConDetalles(direccion: "DIRECCION ANTIGUA", observacion: "OBSERVACION ANTIGUA");
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = new EditarPesaje(
            PesajeId: pesajeId,
            VersionEsperada: 0,
            GanaderoId: gId,
            ClienteId: cId,
            Direccion: "  nueva   direccion  ",
            Secuencia: 1,
            ColorRaza: "BLANCO",
            Sexo: 'M',
            Observacion: "  nueva   observacion  ",
            Motivo: "Correccion de datos");

        var resultado = await servicio.EditarAsync(dto);

        Assert.True(resultado.HuboCambios);
        Assert.Equal(new[] { "Dirección", "Observación" }, resultado.CamposModificados);

        // Verificar en BD
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal("NUEVA DIRECCION", p.Direccion);
            Assert.Equal("NUEVA OBSERVACION", p.Observacion);
            Assert.Equal(1, p.Version);
            Assert.NotNull(p.ModificadoEn);

            // Inmutables
            Assert.Equal(8001, p.NumeroBoleta);
            Assert.Equal(fechaOriginalUtc, p.FechaHora);
            Assert.Equal(110.0m, p.PesoIzquierdo);
            Assert.Equal(115.0m, p.PesoDerecho);
            Assert.Equal(225.0m, p.PesoTotal);
            Assert.Equal(TipoDesposte.ResCompleta, p.TipoDesposte);
            Assert.Equal(OrigenPeso.Balanza, p.OrigenPeso);
            Assert.Equal(u.Id, p.UsuarioId);

            // Auditoría
            var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(AccionAuditoria.Modificar, aud.Accion);
            Assert.Contains("Edición de boleta 8001: Dirección, Observación", aud.Motivo);
            Assert.Contains("CORRECCION DE DATOS", aud.Motivo);

            var antes = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosAntesJson!, JsonOptions);
            var despues = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosDespuesJson!, JsonOptions);
            Assert.Equal("DIRECCION ANTIGUA", antes!.Direccion);
            Assert.Equal("NUEVA DIRECCION", despues!.Direccion);
        }
    }

    [Fact]
    public async Task EditarAsync_CambiarGanaderoYCliente_PersisteYSnapshotTraeMarcaYNombreNuevos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, c1) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int g2, c2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var gan2 = new GanaderoBuilder().ConMarca("BB").ConNombre("GANADERO NUEVO").Build();
            var cli2 = new ClienteBuilder().ConCodigo(222).ConNombre("CLIENTE NUEVO").Build();
            ctx.Ganaderos.Add(gan2);
            ctx.Clientes.Add(cli2);
            await ctx.SaveChangesAsync();
            g2 = gan2.Id;
            c2 = cli2.Id;
        }

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(8002)
                .ConGanaderoId(g1)
                .ConClienteId(c1)
                .ConUsuarioId(u.Id)
                .Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = new EditarPesaje(
            PesajeId: pesajeId,
            VersionEsperada: 0,
            GanaderoId: g2,
            ClienteId: c2,
            Direccion: "DIR",
            Secuencia: 1,
            ColorRaza: "B",
            Sexo: 'M',
            Observacion: "OBS",
            Motivo: null);

        var res = await servicio.EditarAsync(dto);
        Assert.True(res.HuboCambios);
        Assert.Contains("Ganadero", res.CamposModificados);
        Assert.Contains("Cliente", res.CamposModificados);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(g2, p.GanaderoId);
            Assert.Equal(c2, p.ClienteId);

            var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture));
            var antes = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosAntesJson!, JsonOptions);
            var despues = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosDespuesJson!, JsonOptions);

            Assert.Equal("AH", antes!.MarcaGanadero);
            Assert.Equal("GANADERO TEST", antes.NombreGanadero);
            Assert.Equal(100, antes.CodigoCliente);
            Assert.Equal("CLIENTE TEST", antes.NombreCliente);

            Assert.Equal("BB", despues!.MarcaGanadero);
            Assert.Equal("GANADERO NUEVO", despues.NombreGanadero);
            Assert.Equal(222, despues.CodigoCliente);
            Assert.Equal("CLIENTE NUEVO", despues.NombreCliente);
        }
    }

    [Fact]
    public async Task EditarAsync_GanaderoInactivoOInexistente_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, c1) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int gInactivo;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var ganInactivo = new GanaderoBuilder().ConMarca("INAC").ConNombre("INACTIVO").Inactivo().Build();
            ctx.Ganaderos.Add(ganInactivo);
            await ctx.SaveChangesAsync();
            gInactivo = ganInactivo.Id;
        }

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8003).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Cambiar a inactivo
        var dtoInactivo = new EditarPesaje(pesajeId, 0, gInactivo, c1, "DIR", 1, "B", 'M', "OBS", null);
        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() => servicio.EditarAsync(dtoInactivo));
        Assert.Contains(ex1.Errores, e => e.Campo == CamposPesaje.Ganadero);

        // Cambiar a inexistente
        var dtoInexistente = new EditarPesaje(pesajeId, 0, 99999, c1, "DIR", 1, "B", 'M', "OBS", null);
        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() => servicio.EditarAsync(dtoInexistente));
        Assert.Contains(ex2.Errores, e => e.Campo == CamposPesaje.Ganadero);
    }

    [Fact]
    public async Task EditarAsync_MantenerGanaderoActualInactivo_PermiteEdicion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, c1) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int gInactivo, pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var ganInactivo = new GanaderoBuilder().ConMarca("OLD").ConNombre("VIEJO").Inactivo().Build();
            ctx.Ganaderos.Add(ganInactivo);
            await ctx.SaveChangesAsync();
            gInactivo = ganInactivo.Id;

            var p = new PesajeBuilder()
                .ConNumeroBoleta(8004)
                .ConGanaderoId(gInactivo)
                .ConClienteId(c1)
                .ConUsuarioId(u.Id)
                .Build()
                .ConDetalles(direccion: "DIRECCION ANTIGUA");
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Mantiene gInactivo pero edita la dirección
        var dto = new EditarPesaje(pesajeId, 0, gInactivo, c1, "DIRECCION NUEVA", 1, "B", 'M', "OBS", null);
        var res = await servicio.EditarAsync(dto);

        Assert.True(res.HuboCambios);
        Assert.Contains("Dirección", res.CamposModificados);
    }

    [Fact]
    public async Task EditarAsync_SinCambiosReales_RetornaHuboCambiosFalseVersionIntactaYSinAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(8005)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .ConSecuencia(1)
                .Build()
                .ConDetalles(
                    direccion: "CALLE REAL 123",
                    colorRaza: "BLANCO",
                    sexo: 'M',
                    observacion: "SIN NOVEDAD");
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Mismos valores, solo variando espacios y minúsculas que se normalizan a lo mismo
        var dto = new EditarPesaje(
            PesajeId: pesajeId,
            VersionEsperada: 0,
            GanaderoId: gId,
            ClienteId: cId,
            Direccion: "  calle   real   123  ",
            Secuencia: 1,
            ColorRaza: "  blanco  ",
            Sexo: 'm',
            Observacion: "  sin   novedad  ",
            Motivo: "Intento sin cambios");

        var resultado = await servicio.EditarAsync(dto);

        Assert.False(resultado.HuboCambios);
        Assert.Empty(resultado.CamposModificados);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(0, p.Version);
            Assert.Null(p.ModificadoEn);

            var auds = await ctx.Auditorias.Where(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture)).ToListAsync();
            Assert.Empty(auds);
        }
    }

    [Fact]
    public async Task EditarAsync_VersionEsperadaDesfasada_LanzaConcurrenciaException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8006).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Version esperada es 99 cuando en BD es 0
        var dto = new EditarPesaje(pesajeId, 99, gId, cId, "DIR", 1, "B", 'M', "OBS", null);
        await Assert.ThrowsAsync<ConcurrenciaException>(() => servicio.EditarAsync(dto));
    }

    [Fact]
    public async Task EditarAsync_ConflictoRealConcurrencia_LanzaConcurrenciaException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8007).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Simular que otro contexto modificó la boleta (Version pasa de 0 a 1)
        await using (var ctxOtro = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctxOtro.Pesajes.SingleAsync(x => x.Id == pesajeId);
            p.Observacion = "CAMBIO POR OTRO";
            await ctxOtro.SaveChangesAsync();
            Assert.Equal(1, p.Version);
        }

        // El usuario intenta editar teniendo VersionEsperada = 0
        var dto = new EditarPesaje(pesajeId, 0, gId, cId, "DIR NUEVA", 1, "B", 'M', "OBS", null);
        await Assert.ThrowsAsync<ConcurrenciaException>(() => servicio.EditarAsync(dto));
    }

    [Fact]
    public async Task EditarAsync_BoletaAnulada_LanzaReglaNegocioExceptionPesAnulada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8008).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Anulado("YA ANULADA", u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = new EditarPesaje(pesajeId, 0, gId, cId, "DIR", 1, "B", 'M', "OBS", null);
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.EditarAsync(dto));
        Assert.Equal("PES_ANULADA", ex.Codigo);
    }

    [Fact]
    public async Task EditarAsync_BoletaInexistente_LanzaEntidadNoEncontradaException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var dto = new EditarPesaje(99999, 0, 1, 1, "DIR", 1, "B", 'M', "OBS", null);
        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => servicio.EditarAsync(dto));
    }

    [Fact]
    public async Task EditarAsync_ValidacionCampos_LanzaValidacionExceptionSinModificarBd()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var dto = new EditarPesaje(1, 0, gId, cId, new string('A', 151), 0, "B", 'Z', "OBS", null);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.EditarAsync(dto));

        Assert.Contains(ex.Errores, e => e.Campo == CamposPesaje.Direccion);
        Assert.Contains(ex.Errores, e => e.Campo == CamposPesaje.Secuencia);
        Assert.Contains(ex.Errores, e => e.Campo == CamposPesaje.Sexo);
    }

    [Fact]
    public async Task EditarAsync_AtomicidadEstructural_ExactamenteUnaLlamadaAGuardarCambios_YSiFallaNadaPersiste()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);

        var factoryContador = new UowFactoryContadorGuardar(db.UnidadDeTrabajoFactory);
        var (servicio, sesion, _) = CrearServicio(db, uowFactoryOverride: factoryContador);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(8009)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .Build()
                .ConDetalles(direccion: "DIRECCION ORIGINAL");
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // 1. Edición con fallo: llamada a GuardarCambios exactamente 1 vez y lanza
        factoryContador.DebeFallar = true;
        var dto = new EditarPesaje(pesajeId, 0, gId, cId, "DIRECCION MODIFICADA", 1, "B", 'M', "OBS", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EditarAsync(dto));
        Assert.Equal(1, factoryContador.LlamadasGuardar);

        // Nada quedó guardado
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal("DIRECCION ORIGINAL", p.Direccion);
            Assert.Equal(0, p.Version);

            var auds = await ctx.Auditorias.Where(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture)).ToListAsync();
            Assert.Empty(auds);
        }
    }

    [Fact]
    public async Task ObtenerParaEdicionAsync_DevuelveDatosSoloLecturaYListasConActualInactivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(8010)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .ConPesos(120.0m, 125.0m)
                .ConDesposte(TipoDesposte.ResCompleta)
                .ConOrigen(OrigenPeso.Balanza)
                .Build();
            ctx.Pesajes.Add(p);

            // Desactivar el ganadero en BD después de que el pesaje fue creado
            var g = await ctx.Ganaderos.SingleAsync(x => x.Id == gId);
            g.Activo = false;

            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = await servicio.ObtenerParaEdicionAsync(pesajeId);

        Assert.Equal(8010, dto.NumeroBoleta);
        Assert.Equal(120.0m, dto.PesoIzquierdo);
        Assert.Equal(125.0m, dto.PesoDerecho);
        Assert.Equal(245.0m, dto.PesoTotal);
        Assert.Equal(TipoDesposte.ResCompleta, dto.TipoDesposte);
        Assert.Equal(OrigenPeso.Balanza, dto.Origen);
        Assert.Equal(u.NombreCompleto, dto.NombreOperador);

        // La lista de ganaderos incluye al actual aunque esté inactivo
        Assert.Contains(dto.Ganaderos, g => g.Id == gId && !g.Activo);
    }

    [Fact]
    public async Task ObtenerParaEdicionAsync_BoletaAnulada_LanzaReglaNegocioExceptionPesAnulada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8011).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Anulado("YA ANULADA", u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ObtenerParaEdicionAsync(pesajeId));
        Assert.Equal("PES_ANULADA", ex.Codigo);
    }

    [Fact]
    public async Task EditarAsync_NoAfectaConteoImpresionesNiSiguienteNumeroBoleta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(8012).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            // Simular una impresión previa
            ctx.Auditorias.Add(new AuditoriaEntidad
            {
                FechaHora = DateTime.UtcNow,
                UsuarioId = u.Id,
                Accion = AccionAuditoria.Imprimir,
                Entidad = "Pesaje",
                EntidadId = p.Id.ToString(CultureInfo.InvariantCulture)
            });
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        long siguienteAntes;
        int impresionesAntes;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var param = await uow.Parametros.ObtenerAsync();
            siguienteAntes = param.SiguienteNumeroBoleta;
            impresionesAntes = await uow.Auditoria.ContarImpresionesPesajeAsync(pesajeId);
        }

        var dto = new EditarPesaje(pesajeId, 0, gId, cId, "DIR NUEVA", 1, "B", 'M', "OBS NUEVA", null);
        await servicio.EditarAsync(dto);

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var param = await uow.Parametros.ObtenerAsync();
            var impresionesDespues = await uow.Auditoria.ContarImpresionesPesajeAsync(pesajeId);

            Assert.Equal(siguienteAntes, param.SiguienteNumeroBoleta);
            Assert.Equal(impresionesAntes, impresionesDespues);
        }
    }

    // =========================================================================
    // ANULACIÓN
    // =========================================================================

    [Fact]
    public async Task AnularAsync_RolOperador_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var dto = new AnularPesaje(1, 0, "MOTIVO DE ANULACION");
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.AnularAsync(dto));
    }

    [Fact]
    public async Task AnularAsync_Exitoso_CambiaEstadoAnuladoRegistraMotivoYAuditoriaSinTocarPesosNiSiguienteNumero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, tiempo) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(9001)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .ConPesos(115.0m, 120.0m)
                .ConDesposte(TipoDesposte.ResCompleta)
                .Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        long siguienteAntes;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var param = await uow.Parametros.ObtenerAsync();
            siguienteAntes = param.SiguienteNumeroBoleta;
        }

        var dto = new AnularPesaje(pesajeId, 0, "   error   en   pesaje   original   ");
        await servicio.AnularAsync(dto);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(EstadoPesaje.Anulado, p.Estado);
            Assert.Equal("ERROR EN PESAJE ORIGINAL", p.MotivoAnulacion);
            Assert.Equal(u.Id, p.AnuladoPorId);
            Assert.Equal(tiempo.GetUtcNow().UtcDateTime, p.AnuladoEn);
            Assert.Equal(tiempo.GetUtcNow().UtcDateTime, p.ModificadoEn);
            Assert.Equal(1, p.Version);

            // Pesos y número intactos
            Assert.Equal(9001, p.NumeroBoleta);
            Assert.Equal(115.0m, p.PesoIzquierdo);
            Assert.Equal(120.0m, p.PesoDerecho);
            Assert.Equal(235.0m, p.PesoTotal);

            // Auditoría Anular
            var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(AccionAuditoria.Anular, aud.Accion);
            Assert.Equal("Anulación de boleta 9001: ERROR EN PESAJE ORIGINAL", aud.Motivo);

            var antes = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosAntesJson!, JsonOptions);
            var despues = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosDespuesJson!, JsonOptions);
            Assert.Equal(EstadoPesaje.Vigente, antes!.Estado);
            Assert.Equal(EstadoPesaje.Anulado, despues!.Estado);
            Assert.Equal("ERROR EN PESAJE ORIGINAL", despues.MotivoAnulacion);
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var param = await uow.Parametros.ObtenerAsync();
            Assert.Equal(siguienteAntes, param.SiguienteNumeroBoleta);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public async Task AnularAsync_MotivoInvalido_LanzaValidacionException(string motivoInvalido)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var dto = new AnularPesaje(1, 0, motivoInvalido);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.AnularAsync(dto));
        Assert.Contains(ex.Errores, e => e.Campo == CamposGestionPesajes.Motivo);
    }

    [Fact]
    public async Task AnularAsync_BoletaYaAnulada_LanzaReglaNegocioExceptionPesYaAnulada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(9002).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Anulado("YA ANULADA", u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = new AnularPesaje(pesajeId, 0, "OTRO MOTIVO");
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.AnularAsync(dto));
        Assert.Equal("PES_YA_ANULADA", ex.Codigo);
    }

    [Fact]
    public async Task AnularAsync_VersionDesfasada_LanzaConcurrenciaException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(9003).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var dto = new AnularPesaje(pesajeId, 50, "MOTIVO VALIDO");
        await Assert.ThrowsAsync<ConcurrenciaException>(() => servicio.AnularAsync(dto));
    }

    [Fact]
    public async Task AnularAsync_BoletaInexistente_LanzaEntidadNoEncontradaException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var dto = new AnularPesaje(99999, 0, "MOTIVO VALIDO");
        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => servicio.AnularAsync(dto));
    }

    [Fact]
    public async Task AnularAsync_IntegracionConPesajeYBoleta_LiberaSecuenciaYActualizaServicioBoletas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);
        var (servicio, sesion, tiempo) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(9004)
                .ConFechaHora(tiempo.GetUtcNow().UtcDateTime)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(u.Id)
                .ConSecuencia(5)
                .Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Anular boleta
        await servicio.AnularAsync(new AnularPesaje(pesajeId, 0, "ANULANDO SECUENCIA 5"));

        // Tras anular, EditarAsync falla con PES_ANULADA
        var dtoEdicion = new EditarPesaje(pesajeId, 1, gId, cId, "DIR", 5, "B", 'M', "OBS", null);
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.EditarAsync(dtoEdicion));
        Assert.Equal("PES_ANULADA", ex.Codigo);

        // ServicioBoletas devuelve Estado = Anulado y su motivo
        var servicioBoletas = new ServicioBoletas(
            db.UnidadDeTrabajoFactory,
            sesion,
            new PesajeFaeneo.Tests.Utilidades.Boletas.AlmacenLogoFalso(),
            tiempo,
            NullLogger<ServicioBoletas>.Instance);
        var boleta = await servicioBoletas.ObtenerPorIdAsync(pesajeId);
        Assert.Equal(EstadoPesaje.Anulado, boleta.Estado);
        Assert.Equal("ANULANDO SECUENCIA 5", boleta.MotivoAnulacion);

        // SugerirSecuenciaAsync (F4) ya no cuenta la boleta anulada: secuencia máxima vigente vuelve a ser nula -> 1
        var servicioPesajes = new ServicioPesajes(
            db.UnidadDeTrabajoFactory,
            sesion,
            new PesajeFaeneo.Tests.Utilidades.Balanza.ServicioBalanzaFalso(),
            tiempo,
            NullLogger<ServicioPesajes>.Instance);
        var secSugerida = await servicioPesajes.SugerirSecuenciaAsync(gId);
        Assert.Equal(1, secSugerida.Siguiente);
    }

    [Fact]
    public async Task AnularAsync_AtomicidadEstructural_ExactamenteUnaLlamadaAGuardarCambios_YSiFallaNadaPersiste()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId) = await SembrarGanaderoYClienteAsync(db);

        var factoryContador = new UowFactoryContadorGuardar(db.UnidadDeTrabajoFactory);
        var (servicio, sesion, _) = CrearServicio(db, uowFactoryOverride: factoryContador);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(9005).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        factoryContador.DebeFallar = true;
        var dto = new AnularPesaje(pesajeId, 0, "MOTIVO ATOMICIDAD");

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.AnularAsync(dto));
        Assert.Equal(1, factoryContador.LlamadasGuardar);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(EstadoPesaje.Vigente, p.Estado);
            Assert.Null(p.MotivoAnulacion);
            Assert.Equal(0, p.Version);

            var auds = await ctx.Auditorias.Where(a => a.Entidad == "Pesaje" && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture)).ToListAsync();
            Assert.Empty(auds);
        }
    }
}
