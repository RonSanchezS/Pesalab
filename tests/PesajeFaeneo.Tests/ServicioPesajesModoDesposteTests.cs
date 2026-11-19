using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D7): el grabado rechaza un tipo de desposte que el tipo de pesaje configurado no permite.</summary>
public class ServicioPesajesModoDesposteTests
{
    private static ServicioPesajes CrearServicio(BaseDatosEnMemoria db, SesionActual sesion)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(-4)));
        var balanza = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(1, EstadoBalanza.Conectada, 120.0m, true, DateTimeOffset.UtcNow, null, "Conectada", false)
        };
        return new ServicioPesajes(db.UnidadDeTrabajoFactory, sesion, balanza, new PesajeFaeneo.Tests.Utilidades.Licencias.ServicioLicenciaFalso(), tiempo, NullLogger<ServicioPesajes>.Instance);
    }

    private static async Task<(int GanaderoId, int ClienteId)> SembrarAsync(BaseDatosEnMemoria db, ModoDesposte? modo)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConCodigo("1").ConNombre("GANADERO TEST").Build();
        var cliente = new ClienteBuilder().ConCodigo(100).ConNombre("CLIENTE TEST").Build();
        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        (await ctx.ParametrosSistema.SingleAsync()).ModoDesposte = modo;
        await ctx.SaveChangesAsync();
        return (ganadero.Id, cliente.Id);
    }

    private static RegistrarPesaje Dto(int ganaderoId, int clienteId, TipoDesposte tipo) => new(
        GanaderoId: ganaderoId,
        ClienteId: clienteId,
        Direccion: "Calle Test 123",
        Secuencia: 1,
        ColorRaza: "Overo",
        Sexo: 'M',
        TipoDesposte: tipo,
        PesoIzquierdo: 120.0m,
        PesoDerecho: tipo == TipoDesposte.ResCompleta ? 118.5m : 0m,
        Observacion: "Obs",
        CapturadoConSimulador: false);

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.MediaRes)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.ResCompleta)]
    public async Task Registrar_TipoNoPermitido_SeRechazaSinConsumirNumeroNiGrabar(ModoDesposte modo, TipoDesposte tipo)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var servicio = CrearServicio(db, sesion);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db, modo);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.RegistrarAsync(Dto(g, c, tipo)));

        Assert.Equal("PES_DESPOSTE_NO_PERMITIDO", ex.Codigo);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await ctx.Pesajes.CountAsync());
        Assert.Equal(1, (await ctx.ParametrosSistema.SingleAsync()).SiguienteNumeroBoleta);
    }

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.ResCompleta)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.MediaRes)]
    [InlineData(null, TipoDesposte.ResCompleta)]
    [InlineData(null, TipoDesposte.MediaRes)]
    public async Task Registrar_TipoPermitido_Graba(ModoDesposte? modo, TipoDesposte tipo)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var servicio = CrearServicio(db, sesion);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db, modo);

        var r = await servicio.RegistrarAsync(Dto(g, c, tipo));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(tipo, (await ctx.Pesajes.SingleAsync(p => p.Id == r.Id)).TipoDesposte);
    }
}
