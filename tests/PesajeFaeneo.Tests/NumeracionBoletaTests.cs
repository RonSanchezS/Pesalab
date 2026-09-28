using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class NumeracionBoletaTests
{
    [Fact]
    public async Task ReservarNumeroBoletaAsync_SinTransaccion_LanzaInvalidOperationException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var uow = await db.UnidadDeTrabajoFactory.CrearAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.Parametros.ReservarNumeroBoletaAsync());
    }

    [Fact]
    public async Task ReservarNumeroBoletaAsync_ConTransaccion_IncrementaYDevuelveValorAnterior()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        // Configurar contador en 1000
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE ParametrosSistema SET SiguienteNumeroBoleta = 1000 WHERE Id = 1;");
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        await using (var tx = await uow.IniciarTransaccionAsync())
        {
            var reservado = await uow.Parametros.ReservarNumeroBoletaAsync();
            Assert.Equal(1000L, reservado);

            await tx.ConfirmarAsync();
        }

        // Verificar que tras confirmar la fila vale 1001
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var valorActual = await ctx.Database
                .SqlQueryRaw<long>("SELECT SiguienteNumeroBoleta AS Value FROM ParametrosSistema WHERE Id = 1")
                .SingleAsync();
            Assert.Equal(1001L, valorActual);
        }
    }

    [Fact]
    public async Task ReservarNumeroBoletaAsync_TransaccionDesechadaSinConfirmar_RevierteIncremento()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE ParametrosSistema SET SiguienteNumeroBoleta = 1000 WHERE Id = 1;");
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            await using (var tx = await uow.IniciarTransaccionAsync())
            {
                var reservado = await uow.Parametros.ReservarNumeroBoletaAsync();
                Assert.Equal(1000L, reservado);
                // No se llama a ConfirmarAsync() -> rollback al hacer Dispose
            }
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var valorActual = await ctx.Database
                .SqlQueryRaw<long>("SELECT SiguienteNumeroBoleta AS Value FROM ParametrosSistema WHERE Id = 1")
                .SingleAsync();
            Assert.Equal(1000L, valorActual);
        }
    }

    [Fact]
    public async Task ReservarNumeroBoletaAsync_TresReservasEnMismaTransaccion_DevuelveValoresConsecutivos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE ParametrosSistema SET SiguienteNumeroBoleta = 1000 WHERE Id = 1;");
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        await using (var tx = await uow.IniciarTransaccionAsync())
        {
            var num1 = await uow.Parametros.ReservarNumeroBoletaAsync();
            var num2 = await uow.Parametros.ReservarNumeroBoletaAsync();
            var num3 = await uow.Parametros.ReservarNumeroBoletaAsync();

            Assert.Equal(1000L, num1);
            Assert.Equal(1001L, num2);
            Assert.Equal(1002L, num3);

            await tx.ConfirmarAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var valorActual = await ctx.Database
                .SqlQueryRaw<long>("SELECT SiguienteNumeroBoleta AS Value FROM ParametrosSistema WHERE Id = 1")
                .SingleAsync();
            Assert.Equal(1003L, valorActual);
        }
    }
}
