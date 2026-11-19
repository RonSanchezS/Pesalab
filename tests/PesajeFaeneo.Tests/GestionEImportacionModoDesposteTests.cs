using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11: el modo llega al buscador, a la edición de boletas (D6) y a la importación (D10).</summary>
public class GestionEImportacionModoDesposteTests
{
    private static async Task<int> SembrarAsync(BaseDatosEnMemoria db, int usuarioId, ModoDesposte? modo, TipoDesposte tipo)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = new GanaderoBuilder().ConCodigo("7").Build();
        var c = new ClienteBuilder().ConCodigo(101).Build();
        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        (await ctx.ParametrosSistema.SingleAsync()).ModoDesposte = modo;
        await ctx.SaveChangesAsync();

        var p = new PesajeBuilder()
            .ConNumeroBoleta(10)
            .ConFechaHora(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc))
            .ConGanaderoId(g.Id)
            .ConClienteId(c.Id)
            .ConUsuarioId(usuarioId)
            .ConDesposte(tipo)
            .ConPesos(110m, tipo == TipoDesposte.ResCompleta ? 108m : 0m)
            .Build();
        ctx.Pesajes.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static ServicioGestionPesajes CrearServicio(BaseDatosEnMemoria db, SesionActual sesion) =>
        new(db.UnidadDeTrabajoFactory, sesion, new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero)), NullLogger<ServicioGestionPesajes>.Instance);

    [Theory]
    [InlineData(null, ModoDesposte.Ambos)]
    [InlineData(ModoDesposte.SoloResCompleta, ModoDesposte.SoloResCompleta)]
    public async Task Buscar_DevuelveElModoEfectivo(ModoDesposte? guardado, ModoDesposte esperado)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        await SembrarAsync(db, usuario.Id, guardado, TipoDesposte.ResCompleta);

        var resultado = await CrearServicio(db, sesion).BuscarAsync(new FiltroBoletas(null, null, 10, null, null, null));

        Assert.Single(resultado.Boletas);
        Assert.Equal(esperado, resultado.ModoDesposte);
    }

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta, false)]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.MediaRes, true)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.ResCompleta, true)]
    public async Task ObtenerParaEdicion_MostrarDesposteSegunModoYTipo(ModoDesposte modo, TipoDesposte tipo, bool esperado)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var id = await SembrarAsync(db, usuario.Id, modo, tipo);

        var dto = await CrearServicio(db, sesion).ObtenerParaEdicionAsync(id);

        Assert.Equal(esperado, dto.MostrarDesposte);
    }

    [Theory]
    [InlineData(null, ModoDesposte.Ambos)]
    [InlineData(ModoDesposte.SoloMediaRes, ModoDesposte.SoloMediaRes)]
    public async Task Importacion_ObtenerClaves_IncluyeElModoEfectivo(ModoDesposte? guardado, ModoDesposte esperado)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            (await ctx.ParametrosSistema.SingleAsync()).ModoDesposte = guardado;
            await ctx.SaveChangesAsync();
        }

        var claves = await new RepositorioImportacion(db.ContextFactory, NullLogger<RepositorioImportacion>.Instance).ObtenerClavesAsync();

        Assert.Equal(esperado, claves.ModoDesposte);
    }
}
