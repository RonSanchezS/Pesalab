using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioBoletasTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private static FakeTimeProvider CrearTimeProvider(DateTimeOffset? ahora = null)
    {
        var tp = new FakeTimeProvider(ahora ?? new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
        tp.SetLocalTimeZone(ZonaBolivia);
        return tp;
    }

    [Fact]
    public async Task SinSesion_TodosLosMetodosLanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var timeProvider = CrearTimeProvider();
        var logo = new AlmacenLogoFalso();
        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, logo, timeProvider, NullLogger<ServicioBoletas>.Instance);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerPorIdAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerPorNumeroAsync(100));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearMuestraAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.RegistrarImpresionAsync(
            new RegistroImpresion(1, 100, false, 1, "HP", FormatoBoleta.MediaCarta, "PC-01")));
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DevuelveCamposYConvierteFechaHoraLocalCorrectamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var timeProvider = CrearTimeProvider(new DateTimeOffset(2026, 9, 27, 15, 0, 0, TimeSpan.Zero));
        var logo = new AlmacenLogoFalso(@"C:\ProgramData\PesajeFaeneo\logo\logo.png");

        int pesajeId;
        // Fecha en UTC: 2026-09-27 02:30:00 -> en UTC-4 es 2026-09-26 22:30:00
        var fechaUtc = new DateTime(2026, 9, 27, 2, 30, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConMarca("AH").ConNombre("GANADERO TEST").Build();
            var c = new ClienteBuilder().ConCodigo(101).ConNombre("CLIENTE TEST").ConDireccion("AV CENTRAL").Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(12345)
                .ConFechaHora(fechaUtc)
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(usuario.Id)
                .ConSecuencia(5)
                .ConDesposte(TipoDesposte.ResCompleta)
                .ConPesos(130.5m, 125.0m)
                .Build();

            pesaje.Direccion = "AV CENTRAL";
            pesaje.ColorRaza = "NEGRO";
            pesaje.Sexo = 'H';
            pesaje.Observacion = "PESAJE DE NOCHE";

            ctx.Pesajes.Add(pesaje);
            await ctx.SaveChangesAsync();
            pesajeId = pesaje.Id;
        }

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, logo, timeProvider, NullLogger<ServicioBoletas>.Instance);
        var boleta = await servicio.ObtenerPorIdAsync(pesajeId);

        Assert.Equal(pesajeId, boleta.PesajeId);
        Assert.Equal(12345, boleta.NumeroBoleta);
        Assert.Equal(new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified), boleta.FechaHoraLocal);
        Assert.Equal("AH", boleta.MarcaGanadero);
        Assert.Equal("GANADERO TEST", boleta.NombreGanadero);
        Assert.Equal(5, boleta.Secuencia);
        Assert.Equal(101, boleta.CodigoCliente);
        Assert.Equal("CLIENTE TEST", boleta.NombreCliente);
        Assert.Equal("AV CENTRAL", boleta.Direccion);
        Assert.Equal("NEGRO", boleta.ColorRaza);
        Assert.Equal('H', boleta.Sexo);
        Assert.Equal(TipoDesposte.ResCompleta, boleta.TipoDesposte);
        Assert.Equal(130.5m, boleta.PesoIzquierdo);
        Assert.Equal(125.0m, boleta.PesoDerecho);
        Assert.Equal(255.5m, boleta.PesoTotal);
        Assert.Equal("PESAJE DE NOCHE", boleta.Observacion);
        Assert.Equal(OrigenPeso.Balanza, boleta.Origen);
        Assert.Equal(EstadoPesaje.Vigente, boleta.Estado);
        Assert.Equal(usuario.NombreCompleto, boleta.NombreOperador);
        Assert.Equal(@"C:\ProgramData\PesajeFaeneo\logo\logo.png", boleta.Empresa.RutaLogo);
        Assert.Equal(0, boleta.ImpresionesPrevias);
        Assert.False(boleta.EsReimpresion);
        Assert.False(boleta.EsMuestra);
    }

    [Fact]
    public async Task Fleteros_ActivosOrdenadosPorOrdenYSinInactivos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            ctx.Fleteros.Add(new Fletero { Orden = 3, Nombre = "F3", Celular = "333", Activo = true });
            ctx.Fleteros.Add(new Fletero { Orden = 1, Nombre = "F1", Celular = "111", Activo = true });
            ctx.Fleteros.Add(new Fletero { Orden = 2, Nombre = "F2", Celular = "222", Activo = true });
            ctx.Fleteros.Add(new Fletero { Orden = 4, Nombre = "F4", Celular = "444", Activo = false });

            var p = new PesajeBuilder().ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(usuario.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);
        var boleta = await servicio.ObtenerPorIdAsync(pesajeId);

        Assert.Equal(3, boleta.Fleteros.Count);
        Assert.Equal(1, boleta.Fleteros[0].Orden);
        Assert.Equal("F1", boleta.Fleteros[0].Nombre);
        Assert.Equal(2, boleta.Fleteros[1].Orden);
        Assert.Equal("F2", boleta.Fleteros[1].Nombre);
        Assert.Equal(3, boleta.Fleteros[2].Orden);
        Assert.Equal("F3", boleta.Fleteros[2].Nombre);
    }

    [Fact]
    public async Task Logo_SinLogo_RutaLogoEsNull()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var p = new PesajeBuilder().ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(usuario.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var logoFalso = new AlmacenLogoFalso(null);
        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, logoFalso, CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);
        var boleta = await servicio.ObtenerPorIdAsync(pesajeId);

        Assert.Null(boleta.Empresa.RutaLogo);
    }

    [Fact]
    public async Task ImpresionesPrevias_CicloDeImpresionYReimpresion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int pesajeId;
        long numBoleta = 8888;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var p = new PesajeBuilder().ConNumeroBoleta(numBoleta).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(usuario.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);

        var boletaInicial = await servicio.ObtenerPorIdAsync(pesajeId);
        Assert.Equal(0, boletaInicial.ImpresionesPrevias);
        Assert.False(boletaInicial.EsReimpresion);

        // Registrar primera impresión
        await servicio.RegistrarImpresionAsync(new RegistroImpresion(
            pesajeId, numBoleta, EsReimpresion: false, Copias: 1, "Impresora 1", FormatoBoleta.MediaCarta, "PC-01"));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.Pesaje && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(AccionAuditoria.Imprimir, aud.Accion);
            Assert.Contains("8888", aud.Motivo);
        }

        var boletaTrasPrimera = await servicio.ObtenerPorIdAsync(pesajeId);
        Assert.Equal(1, boletaTrasPrimera.ImpresionesPrevias);
        Assert.True(boletaTrasPrimera.EsReimpresion);

        // Registrar reimpresión
        await servicio.RegistrarImpresionAsync(new RegistroImpresion(
            pesajeId, numBoleta, EsReimpresion: true, Copias: 2, "Impresora 1", FormatoBoleta.MediaCarta, "PC-01"));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var totalAud = await ctx.Auditorias.CountAsync(a => a.Entidad == EntidadesAuditoria.Pesaje && a.EntidadId == pesajeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(2, totalAud);
        }

        var boletaTrasSegunda = await servicio.ObtenerPorIdAsync(pesajeId);
        Assert.Equal(2, boletaTrasSegunda.ImpresionesPrevias);
        Assert.True(boletaTrasSegunda.EsReimpresion);
    }

    [Fact]
    public async Task ObtenerPorNumeroAsync_NumeroInvalidoOInexistente_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);

        var exCero = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ObtenerPorNumeroAsync(0));
        Assert.Contains(exCero.Errores, e => e.Campo == CamposBoleta.Numero);

        var exInexistente = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ObtenerPorNumeroAsync(999999));
        Assert.Contains(exInexistente.Errores, e => e.Campo == CamposBoleta.Numero && e.Mensaje.Contains("999999"));
    }

    [Fact]
    public async Task ObtenerPorIdAsync_PesajeAnuladoYSimulado_MapeaEstadoYOrigen()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var p = new PesajeBuilder()
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(usuario.Id)
                .ConOrigen(OrigenPeso.Simulador)
                .Anulado("Error en tara", usuario.Id)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);
        var boleta = await servicio.ObtenerPorIdAsync(pesajeId);

        Assert.Equal(EstadoPesaje.Anulado, boleta.Estado);
        Assert.Equal("Error en tara", boleta.MotivoAnulacion);
        Assert.Equal(OrigenPeso.Simulador, boleta.Origen);
    }

    [Fact]
    public async Task CrearMuestraAsync_DevuelveDatosFicticiosConEmpresaRealYSinAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);
        var muestra = await servicio.CrearMuestraAsync();

        Assert.True(muestra.EsMuestra);
        Assert.Equal(0, muestra.PesajeId);
        Assert.Equal(0, muestra.NumeroBoleta);
        Assert.Equal("NOMBRE DE EMPRESA", muestra.Empresa.Nombre);
        Assert.Equal(usuario.NombreCompleto, muestra.NombreOperador);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var totalAud = await ctx.Auditorias.CountAsync();
        Assert.Equal(0, totalAud);
    }

    [Fact]
    public async Task Operador_TienePermisoParaObtenerYRegistrarImpresiones()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var p = new PesajeBuilder().ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(usuario.Id).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        var servicio = new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), CrearTimeProvider(), NullLogger<ServicioBoletas>.Instance);

        // Operador puede obtener boleta
        var b = await servicio.ObtenerPorIdAsync(pesajeId);
        Assert.NotNull(b);

        // Operador puede registrar impresión
        await servicio.RegistrarImpresionAsync(new RegistroImpresion(pesajeId, b.NumeroBoleta, false, 1, "HP", FormatoBoleta.MediaCarta, "PC-01"));

        // Operador puede reimprimir
        await servicio.RegistrarImpresionAsync(new RegistroImpresion(pesajeId, b.NumeroBoleta, true, 1, "HP", FormatoBoleta.MediaCarta, "PC-01"));
    }
}
