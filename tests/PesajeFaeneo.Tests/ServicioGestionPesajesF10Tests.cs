using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
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

namespace PesajeFaeneo.Tests;

/// <summary>F10 (O5): el fletero es un dato descriptivo de la boleta que se puede editar y se audita.</summary>
public class ServicioGestionPesajesF10Tests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static (ServicioGestionPesajes Servicio, SesionActual Sesion) CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        return (new ServicioGestionPesajes(db.UnidadDeTrabajoFactory, sesion, tiempo, NullLogger<ServicioGestionPesajes>.Instance), sesion);
    }

    private sealed record Escenario(int PesajeId, int GanaderoId, int ClienteId, int Fletero1, int Fletero2, int FleteroInactivo);

    private static async Task<Escenario> SembrarAsync(BaseDatosEnMemoria db, int usuarioId, bool conFletero1)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = new GanaderoBuilder().ConCodigo("1").Build();
        var c = new ClienteBuilder().ConCodigo(100).Build();
        var f1 = new Fletero { Codigo = 1, Nombre = "FLETERO UNO", Celular = "70000001", Activo = true, CreadoEn = DateTime.UtcNow };
        var f2 = new Fletero { Codigo = 2, Nombre = "FLETERO DOS", Celular = "70000002", Activo = true, CreadoEn = DateTime.UtcNow };
        var fi = new Fletero { Codigo = 3, Nombre = "FLETERO INACTIVO", Celular = "70000003", Activo = false, CreadoEn = DateTime.UtcNow };
        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Fleteros.AddRange(f1, f2, fi);
        await ctx.SaveChangesAsync();

        var p = new PesajeBuilder().ConNumeroBoleta(9001).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(usuarioId).Build();
        p.Direccion = string.Empty;
        p.ColorRaza = string.Empty;
        p.Observacion = string.Empty;
        p.Sexo = 'M';
        p.Secuencia = 1;
        p.FleteroId = conFletero1 ? f1.Id : null;
        ctx.Pesajes.Add(p);
        await ctx.SaveChangesAsync();
        return new Escenario(p.Id, g.Id, c.Id, f1.Id, f2.Id, fi.Id);
    }

    private static EditarPesaje Edicion(Escenario e, int? fleteroId, int version = 0) => new(
        PesajeId: e.PesajeId,
        VersionEsperada: version,
        GanaderoId: e.GanaderoId,
        ClienteId: e.ClienteId,
        Direccion: "",
        Secuencia: 1,
        ColorRaza: "",
        Sexo: 'M',
        Observacion: "",
        Motivo: null,
        FleteroId: fleteroId);

    [Fact]
    public async Task EditarAsync_AsignarFletero_PersisteLoAuditaYLoReportaComoCampoModificado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var e = await SembrarAsync(db, u.Id, conFletero1: false);

        var resultado = await servicio.EditarAsync(Edicion(e, e.Fletero2));

        Assert.True(resultado.HuboCambios);
        Assert.Equal(new[] { "Fletero" }, resultado.CamposModificados);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == e.PesajeId);
        Assert.Equal(e.Fletero2, p.FleteroId);
        Assert.Equal(1, p.Version);

        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == "Pesaje" && a.EntidadId == e.PesajeId.ToString(CultureInfo.InvariantCulture));
        Assert.Contains("Fletero", aud.Motivo);
        var antes = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosAntesJson!, JsonOptions)!;
        var despues = JsonSerializer.Deserialize<PesajeAuditoria>(aud.DatosDespuesJson!, JsonOptions)!;
        Assert.Null(antes.FleteroId);
        Assert.Equal(e.Fletero2, despues.FleteroId);
        Assert.Equal("FLETERO DOS", despues.NombreFletero);
        Assert.Equal(2, despues.CodigoFletero);
    }

    [Fact]
    public async Task EditarAsync_CambiarDeFleteroYQuitarlo_AuditaAntesYDespues()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var e = await SembrarAsync(db, u.Id, conFletero1: true);

        await servicio.EditarAsync(Edicion(e, e.Fletero2, version: 0));
        var resultado = await servicio.EditarAsync(Edicion(e, fleteroId: null, version: 1));

        Assert.Equal(new[] { "Fletero" }, resultado.CamposModificados);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Null((await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == e.PesajeId)).FleteroId);

        var auditorias = await ctx.Auditorias.Where(a => a.Entidad == "Pesaje").OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, auditorias.Count);
        var primera = JsonSerializer.Deserialize<PesajeAuditoria>(auditorias[0].DatosAntesJson!, JsonOptions)!;
        Assert.Equal("FLETERO UNO", primera.NombreFletero);
        var segunda = JsonSerializer.Deserialize<PesajeAuditoria>(auditorias[1].DatosDespuesJson!, JsonOptions)!;
        Assert.Null(segunda.FleteroId);
        Assert.Null(segunda.NombreFletero);
    }

    [Fact]
    public async Task EditarAsync_MismoFletero_NoHayCambiosNiAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var e = await SembrarAsync(db, u.Id, conFletero1: true);

        var resultado = await servicio.EditarAsync(Edicion(e, e.Fletero1));

        Assert.False(resultado.HuboCambios);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(ctx.Auditorias.Where(a => a.Entidad == "Pesaje"));
        Assert.Equal(0, (await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == e.PesajeId)).Version);
    }

    [Fact]
    public async Task EditarAsync_FleteroInactivoOInexistente_LanzaValidacion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var e = await SembrarAsync(db, u.Id, conFletero1: false);

        var exInactivo = await Assert.ThrowsAsync<ValidacionException>(() => servicio.EditarAsync(Edicion(e, e.FleteroInactivo)));
        Assert.Contains(exInactivo.Errores, x => x.Campo == CamposPesaje.Fletero);

        var exInexistente = await Assert.ThrowsAsync<ValidacionException>(() => servicio.EditarAsync(Edicion(e, 9999)));
        Assert.Contains(exInexistente.Errores, x => x.Campo == CamposPesaje.Fletero);
    }

    [Fact]
    public async Task ObtenerParaEdicionAsync_IncluyeFleterosActivosYElActualAunqueEsteInactivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var e = await SembrarAsync(db, u.Id, conFletero1: false);

        // Se desactiva el fletero asignado al pesaje: sigue apareciendo para poder mostrarlo.
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.SingleAsync(x => x.Id == e.PesajeId);
            p.FleteroId = e.FleteroInactivo;
            await ctx.SaveChangesAsync();
        }

        var dto = await servicio.ObtenerParaEdicionAsync(e.PesajeId);

        Assert.Equal(e.FleteroInactivo, dto.FleteroId);
        Assert.NotNull(dto.Fleteros);
        Assert.Contains(dto.Fleteros, f => f.Id == e.Fletero1 && f.Activo);
        Assert.Contains(dto.Fleteros, f => f.Id == e.Fletero2 && f.Activo);
        Assert.Contains(dto.Fleteros, f => f.Id == e.FleteroInactivo && !f.Activo);
    }

    [Fact]
    public async Task BuscarAsync_TraeElNombreDelFletero_YElCodigoDelGanadero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        await SembrarAsync(db, u.Id, conFletero1: true);

        var resultado = await servicio.BuscarAsync(new FiltroBoletas(null, null, 9001, null, null, null));

        var boleta = Assert.Single(resultado.Boletas);
        Assert.Equal("FLETERO UNO", boleta.NombreFletero);
        Assert.Equal("1", boleta.Codigo);
    }
}
