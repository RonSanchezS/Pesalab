using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioPesajesConcurrenciaTests : IClassFixture<SqliteArchivoFixture>
{
    private readonly SqliteArchivoFixture _fixture;

    public ServicioPesajesConcurrenciaTests(SqliteArchivoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Concurrencia_DiezRegistrosSimultaneos_NumerosDistintosYConsecutivos()
    {
        var uowFactory = new UnidadDeTrabajoFactory(_fixture.ContextFactory);
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(_fixture.ContextFactory, sesion, Rol.Operador, "operador_concurrente");

        int ganaderoId;
        int clienteId;
        await using (var ctx = await _fixture.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConMarca("CC").ConNombre("GANADERO CONCURRENTE").Build();
            var c = new ClienteBuilder().ConCodigo(888).ConNombre("CLIENTE CONCURRENTE").Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();
            ganaderoId = g.Id;
            clienteId = c.Id;
        }

        var balanza = new ServicioBalanzaFalso
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
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4)));
        var servicio = new ServicioPesajes(uowFactory, sesion, balanza, tiempo, NullLogger<ServicioPesajes>.Instance);

        var dto = new RegistrarPesaje(
            ganaderoId,
            clienteId,
            "Direccion",
            1,
            "Raza",
            'M',
            TipoDesposte.ResCompleta,
            100m,
            100m,
            null,
            false);

        // Lanzar 10 concurrentes
        var tareas = Enumerable.Range(0, 10).Select(_ => Task.Run(() => servicio.RegistrarAsync(dto))).ToArray();
        var resultados = await Task.WhenAll(tareas);

        var boletas = resultados.Select(r => r.NumeroBoleta).OrderBy(n => n).ToList();
        Assert.Equal(10, boletas.Distinct().Count());

        // Verificar que son consecutivas
        var primera = boletas.First();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(primera + i, boletas[i]);
        }

        await using (var ctx = await _fixture.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.ParametrosSistema.FindAsync(1);
            Assert.Equal(primera + 10, p!.SiguienteNumeroBoleta);
        }
    }
}
