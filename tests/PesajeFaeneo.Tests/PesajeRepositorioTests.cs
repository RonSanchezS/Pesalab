using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class PesajeRepositorioTests
{
    private static async Task<(int Ganadero1Id, int Ganadero2Id, int ClienteId, int UsuarioId)> SembrarCatalogosAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g1 = new GanaderoBuilder().ConCodigo("G1").ConNombre("Ganadero 1").Build();
        var g2 = new GanaderoBuilder().ConCodigo("G2").ConNombre("Ganadero 2").Build();
        var c = new ClienteBuilder().ConCodigo(9001).ConNombre("Cliente 9001").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("pesador_repositorio").Build();

        ctx.Ganaderos.AddRange(g1, g2);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g1.Id, g2.Id, c.Id, u.Id);
    }

    [Fact]
    public async Task ObtenerSecuenciaMaximaAsync_FiltraPorGanaderoEstadoYRango()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1Id, g2Id, cId, uId) = await SembrarCatalogosAsync(db);

        var desdeUtc = new DateTime(2026, 9, 26, 4, 0, 0, DateTimeKind.Utc);
        var hastaUtc = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            // Ganadero 1, vigentes dentro del rango: secuencias 1, 2, 5
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(101).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(1).ConFechaHora(desdeUtc.AddHours(2)).Build());
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(102).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(2).ConFechaHora(desdeUtc.AddHours(4)).Build());
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(103).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(5).ConFechaHora(desdeUtc.AddHours(6)).Build());

            // Ganadero 1, ANULADO dentro del rango: secuencia 9
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(104).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(9).ConFechaHora(desdeUtc.AddHours(8)).Anulado("Error", uId).Build());

            // Ganadero 2 (otro ganadero), vigente en rango: secuencia 7
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(105).ConGanaderoId(g2Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(7).ConFechaHora(desdeUtc.AddHours(3)).Build());

            // Ganadero 1, vigente FUERA del rango: secuencia 8
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(106).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(8).ConFechaHora(hastaUtc.AddHours(1)).Build());

            await uow.GuardarCambiosAsync();
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var maxG1 = await uow.Pesajes.ObtenerSecuenciaMaximaAsync(g1Id, desdeUtc, hastaUtc);
            Assert.Equal(5, maxG1);

            var cuentaG1 = await uow.Pesajes.ContarVigentesAsync(g1Id, desdeUtc, hastaUtc);
            Assert.Equal(3, cuentaG1);

            // Ganadero sin pesajes en rango devuelve null y 0
            var maxSinPesajes = await uow.Pesajes.ObtenerSecuenciaMaximaAsync(9999, desdeUtc, hastaUtc);
            Assert.Null(maxSinPesajes);

            var cuentaSinPesajes = await uow.Pesajes.ContarVigentesAsync(9999, desdeUtc, hastaUtc);
            Assert.Equal(0, cuentaSinPesajes);
        }
    }

    [Fact]
    public async Task LimiteRango_DesdeUtcEntra_HastaUtcNoEntra()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1Id, _, cId, uId) = await SembrarCatalogosAsync(db);

        var desdeUtc = new DateTime(2026, 9, 26, 4, 0, 0, DateTimeKind.Utc);
        var hastaUtc = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            // Exactamente en DesdeUtc -> DEBE entrar
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(201).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(3).ConFechaHora(desdeUtc).Build());

            // Exactamente en HastaUtc -> NO debe entrar (rango semiabierto [desde, hasta))
            uow.Pesajes.Agregar(new PesajeBuilder().ConNumeroBoleta(202).ConGanaderoId(g1Id).ConClienteId(cId).ConUsuarioId(uId).ConSecuencia(10).ConFechaHora(hastaUtc).Build());

            await uow.GuardarCambiosAsync();
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var max = await uow.Pesajes.ObtenerSecuenciaMaximaAsync(g1Id, desdeUtc, hastaUtc);
            Assert.Equal(3, max);

            var cuenta = await uow.Pesajes.ContarVigentesAsync(g1Id, desdeUtc, hastaUtc);
            Assert.Equal(1, cuenta);
        }
    }

    [Fact]
    public async Task ListarPorRangoAsync_RespetaOrdenDescendenteMaximoEIncluyeNavegaciones()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1Id, _, cId, uId) = await SembrarCatalogosAsync(db);

        var desdeUtc = new DateTime(2026, 9, 26, 4, 0, 0, DateTimeKind.Utc);
        var hastaUtc = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            for (var i = 1; i <= 5; i++)
            {
                uow.Pesajes.Agregar(new PesajeBuilder()
                    .ConNumeroBoleta(300 + i)
                    .ConGanaderoId(g1Id)
                    .ConClienteId(cId)
                    .ConUsuarioId(uId)
                    .ConSecuencia(i)
                    .ConFechaHora(desdeUtc.AddHours(i))
                    .Build());
            }

            await uow.GuardarCambiosAsync();
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            // Pedir máximo 3
            var lista = await uow.Pesajes.ListarPorRangoAsync(desdeUtc, hastaUtc, 3);

            Assert.Equal(3, lista.Count);
            // Orden descendente por NumeroBoleta: 305, 304, 303
            Assert.Equal(305L, lista[0].NumeroBoleta);
            Assert.Equal(304L, lista[1].NumeroBoleta);
            Assert.Equal(303L, lista[2].NumeroBoleta);

            // Verifica navegaciones cargadas
            Assert.NotNull(lista[0].Ganadero);
            Assert.Equal("G1", lista[0].Ganadero.Codigo);
            Assert.NotNull(lista[0].Cliente);
            Assert.Equal("Cliente 9001", lista[0].Cliente.Nombre);
        }
    }

    [Fact]
    public async Task ObtenerPorIdAsync_DevuelvePesajeConNavegacionesSinTracking()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1Id, _, cId, uId) = await SembrarCatalogosAsync(db);

        int idGenerado;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(401)
                .ConGanaderoId(g1Id)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .ConPesos(110m, 115m)
                .Build();
            uow.Pesajes.Agregar(p);
            await uow.GuardarCambiosAsync();
            idGenerado = p.Id;
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var recuperado = await uow.Pesajes.ObtenerPorIdAsync(idGenerado);
            Assert.NotNull(recuperado);
            Assert.Equal(401L, recuperado.NumeroBoleta);
            Assert.Equal(225.0m, recuperado.PesoTotal);
            Assert.NotNull(recuperado.Ganadero);
            Assert.Equal("G1", recuperado.Ganadero.Codigo);
            Assert.NotNull(recuperado.Cliente);
            Assert.Equal("Cliente 9001", recuperado.Cliente.Nombre);
        }
    }
}
