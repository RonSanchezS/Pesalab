using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

public class PlanConsultasReportesFixture : IAsyncLifetime
{
    public BaseDatosEnMemoria SinEstadisticas { get; private set; } = null!;
    public BaseDatosEnMemoria ConEstadisticas { get; private set; } = null!;

    public int GanaderoId3Sin { get; private set; }
    public int ClienteId5Sin { get; private set; }
    public int GanaderoId3Con { get; private set; }
    public int ClienteId5Con { get; private set; }

    public DateTime DesdeUtc { get; } = new(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc);
    public DateTime HastaUtc { get; } = new(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        SinEstadisticas = await BaseDatosEnMemoria.CrearAsync();
        ConEstadisticas = await BaseDatosEnMemoria.CrearAsync();

        var (gSin, cSin) = await SembrarAsync(SinEstadisticas);
        GanaderoId3Sin = gSin;
        ClienteId5Sin = cSin;

        var (gCon, cCon) = await SembrarAsync(ConEstadisticas);
        GanaderoId3Con = gCon;
        ClienteId5Con = cCon;

        await new DatabaseInitializer(ConEstadisticas.ContextFactory).InicializarAsync();
    }

    private static async Task<(int GanaderoId3, int ClienteId5)> SembrarAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var usuario = new UsuarioBuilder().ConNombreUsuario("plan_user").Build();
        ctx.Usuarios.Add(usuario);

        var ganaderos = new List<Ganadero>(60);
        for (int i = 1; i <= 60; i++)
        {
            ganaderos.Add(new GanaderoBuilder().ConCodigo("G" + i).Build());
        }
        ctx.Ganaderos.AddRange(ganaderos);

        var clientes = new List<Cliente>(40);
        for (int i = 1; i <= 40; i++)
        {
            clientes.Add(new ClienteBuilder().ConCodigo(i).Build());
        }
        ctx.Clientes.AddRange(clientes);

        await ctx.SaveChangesAsync();

        var baseFecha = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var pesajes = new List<Pesaje>(3000);

        for (int i = 1; i <= 3000; i++)
        {
            var builder = new PesajeBuilder()
                .ConNumeroBoleta(i)
                .ConFechaHora(baseFecha.AddMinutes(i * 20))
                .ConGanaderoId(ganaderos[i % 60].Id)
                .ConClienteId(clientes[i % 40].Id)
                .ConUsuarioId(usuario.Id);

            if (i % 100 == 0)
            {
                builder.Anulado("ERROR DE PRUEBA", usuario.Id);
            }

            pesajes.Add(builder.Build());
        }

        ctx.Pesajes.AddRange(pesajes);
        await ctx.SaveChangesAsync();

        return (ganaderos[3].Id, clientes[5].Id);
    }

    public async Task DisposeAsync()
    {
        await SinEstadisticas.DisposeAsync();
        await ConEstadisticas.DisposeAsync();
    }
}

public class PlanConsultasReportesTests : IClassFixture<PlanConsultasReportesFixture>
{
    private readonly PlanConsultasReportesFixture _fixture;

    public PlanConsultasReportesTests(PlanConsultasReportesFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<IReadOnlyList<string>> ObtenerPlanAsync(BaseDatosEnMemoria db, CriteriosReporte criterios)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var repo = new PesajeRepositorio(ctx);
        var consulta = repo.ConsultaReporte(criterios, ReglasReportes.MaximoFilas + 1);
        var conexion = (SqliteConnection)ctx.Database.GetDbConnection();
        return await PlanConsultaSqlite.ObtenerAsync(conexion, consulta);
    }

    private static async Task<string> ObtenerLineaPesajesAsync(BaseDatosEnMemoria db, CriteriosReporte criterios)
    {
        var plan = await ObtenerPlanAsync(db, criterios);
        return PlanConsultaSqlite.LineaPesajes(plan);
    }

    [Fact]
    public async Task Test1_SinEstadisticas_VigentesPorRango_UsaIndiceFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.SinEstadisticas, criterios);

        Assert.True(
            linea.StartsWith("SEARCH p USING INDEX IX_Pesajes_Estado_FechaHora", StringComparison.Ordinal) ||
            linea.StartsWith("SEARCH p USING INDEX IX_Pesajes_FechaHora", StringComparison.Ordinal),
            $"Línea inesperada: {linea}");
    }

    [Fact]
    public async Task Test2_SinEstadisticas_PorGanadero_UsaIndiceGanaderoFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, _fixture.GanaderoId3Sin, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.SinEstadisticas, criterios);

        Assert.StartsWith("SEARCH p USING INDEX IX_Pesajes_GanaderoId_FechaHora", linea);
    }

    [Fact]
    public async Task Test3_SinEstadisticas_Anuladas_UsaIndiceEstadoFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Anulado, null, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.SinEstadisticas, criterios);

        Assert.StartsWith("SEARCH p USING INDEX IX_Pesajes_Estado_FechaHora", linea);
    }

    [Fact]
    public async Task Test4_TablaSqliteStat1_ExisteSoloEnConEstadisticas()
    {
        await using (var ctxCon = await _fixture.ConEstadisticas.ContextFactory.CreateDbContextAsync())
        {
            var cmd = ctxCon.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_stat1';";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(1L, count);
        }

        await using (var ctxSin = await _fixture.SinEstadisticas.ContextFactory.CreateDbContextAsync())
        {
            var cmd = ctxSin.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_stat1';";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(0L, count);
        }
    }

    [Fact]
    public async Task Test5_ConEstadisticas_Rango_UsaIndiceFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.ConEstadisticas, criterios);

        Assert.True(
            linea.StartsWith("SEARCH p USING INDEX IX_Pesajes_Estado_FechaHora", StringComparison.Ordinal) ||
            linea.StartsWith("SEARCH p USING INDEX IX_Pesajes_FechaHora", StringComparison.Ordinal),
            $"Línea inesperada: {linea}");
    }

    [Fact]
    public async Task Test6_ConEstadisticas_Ganadero_UsaIndiceGanaderoFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, _fixture.GanaderoId3Con, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.ConEstadisticas, criterios);

        Assert.StartsWith("SEARCH p USING INDEX IX_Pesajes_GanaderoId_FechaHora", linea);
    }

    [Fact]
    public async Task Test7_ConEstadisticas_Cliente_UsaIndiceClienteFecha()
    {
        // Nota: sin estadísticas, SQLite usa IX_Pesajes_Estado_FechaHora (recorre todas las vigentes del rango).
        // Con estadísticas generadas por ANALYZE, SQLite elige óptimamente IX_Pesajes_ClienteId_FechaHora (D16).
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, _fixture.ClienteId5Con);
        var linea = await ObtenerLineaPesajesAsync(_fixture.ConEstadisticas, criterios);

        Assert.StartsWith("SEARCH p USING INDEX IX_Pesajes_ClienteId_FechaHora", linea);
    }

    [Fact]
    public async Task Test8_ConEstadisticas_Anuladas_UsaIndiceEstadoFecha()
    {
        var criterios = new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Anulado, null, null);
        var linea = await ObtenerLineaPesajesAsync(_fixture.ConEstadisticas, criterios);

        Assert.StartsWith("SEARCH p USING INDEX IX_Pesajes_Estado_FechaHora", linea);
    }

    [Fact]
    public async Task Test9_NingunPlanRealizaScanCompleto()
    {
        var consultas = new List<(BaseDatosEnMemoria Db, CriteriosReporte Criterios)>
        {
            (_fixture.SinEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, null)),
            (_fixture.SinEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, _fixture.GanaderoId3Sin, null)),
            (_fixture.SinEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Anulado, null, null)),
            (_fixture.ConEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, null)),
            (_fixture.ConEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, _fixture.GanaderoId3Con, null)),
            (_fixture.ConEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Vigente, null, _fixture.ClienteId5Con)),
            (_fixture.ConEstadisticas, new CriteriosReporte(_fixture.DesdeUtc, _fixture.HastaUtc, EstadoPesaje.Anulado, null, null))
        };

        foreach (var (db, crit) in consultas)
        {
            var plan = await ObtenerPlanAsync(db, crit);
            Assert.DoesNotContain(plan, d => d == "SCAN p" || d.StartsWith("SCAN p ", StringComparison.Ordinal));
        }
    }
}
