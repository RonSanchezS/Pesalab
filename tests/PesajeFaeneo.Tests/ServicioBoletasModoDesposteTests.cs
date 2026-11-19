using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D6): la boleta omite el desposte solo cuando coincide con el único tipo que pesa la empresa.</summary>
public class ServicioBoletasModoDesposteTests
{
    private static ServicioBoletas CrearServicio(BaseDatosEnMemoria db, SesionActual sesion)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        return new ServicioBoletas(db.UnidadDeTrabajoFactory, sesion, new AlmacenLogoFalso(), tiempo, NullLogger<ServicioBoletas>.Instance);
    }

    private static async Task FijarModoAsync(BaseDatosEnMemoria db, ModoDesposte? modo)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        (await ctx.ParametrosSistema.SingleAsync()).ModoDesposte = modo;
        await ctx.SaveChangesAsync();
    }

    private static async Task<int> SembrarPesajeAsync(BaseDatosEnMemoria db, int usuarioId, TipoDesposte tipo)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = new GanaderoBuilder().ConCodigo("AH").Build();
        var c = new ClienteBuilder().ConCodigo(101).Build();
        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        await ctx.SaveChangesAsync();

        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(500)
            .ConGanaderoId(g.Id)
            .ConClienteId(c.Id)
            .ConUsuarioId(usuarioId)
            .ConDesposte(tipo)
            .ConPesos(120m, tipo == TipoDesposte.ResCompleta ? 118m : 0m)
            .Build();
        ctx.Pesajes.Add(pesaje);
        await ctx.SaveChangesAsync();
        return pesaje.Id;
    }

    [Theory]
    [InlineData(null, TipoDesposte.ResCompleta, true)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.MediaRes, true)]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta, false)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes, false)]
    // Boleta anterior al cambio de modo (o importada): su tipo distingue y se imprime.
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.MediaRes, true)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.ResCompleta, true)]
    public async Task ObtenerPorId_MostrarDesposteSegunModoYTipo(ModoDesposte? modo, TipoDesposte tipo, bool esperado)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        await FijarModoAsync(db, modo);
        var id = await SembrarPesajeAsync(db, usuario.Id, tipo);

        var boleta = await CrearServicio(db, sesion).ObtenerPorIdAsync(id);

        Assert.Equal(tipo, boleta.TipoDesposte);
        Assert.Equal(esperado, boleta.MostrarDesposte);
    }

    [Theory]
    [InlineData(null, TipoDesposte.ResCompleta, true, 238.5)]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta, false, 238.5)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes, false, 120.0)]
    public async Task Muestra_UsaElTipoQuePesaLaEmpresa(ModoDesposte? modo, TipoDesposte tipo, bool mostrar, double total)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await FijarModoAsync(db, modo);

        var muestra = await CrearServicio(db, sesion).CrearMuestraAsync();

        Assert.Equal(tipo, muestra.TipoDesposte);
        Assert.Equal(mostrar, muestra.MostrarDesposte);
        Assert.Equal((decimal)total, muestra.PesoTotal);
        Assert.Equal(muestra.PesoIzquierdo + muestra.PesoDerecho, muestra.PesoTotal);
    }
}
