using System.Globalization;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class BoletaRepositorioTests
{
    private static async Task<(int GanaderoId, int ClienteId, int UsuarioId)> SembrarCatalogosAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g = new GanaderoBuilder().ConMarca("AH").ConNombre("Ganadero AH").Build();
        var c = new ClienteBuilder().ConCodigo(123).ConNombre("Cliente 123").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("pesador_boleta").ConNombreCompleto("Juan Perez").Build();

        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g.Id, c.Id, u.Id);
    }

    [Fact]
    public async Task ObtenerParaBoletaPorIdAsync_IncluyeGanaderoClienteYUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(5001)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            uow.Pesajes.Agregar(pesaje);
            await uow.GuardarCambiosAsync();
            pesajeId = pesaje.Id;
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var encontrado = await uow.Pesajes.ObtenerParaBoletaPorIdAsync(pesajeId);

            Assert.NotNull(encontrado);
            Assert.NotNull(encontrado.Ganadero);
            Assert.Equal("AH", encontrado.Ganadero.Marca);
            Assert.NotNull(encontrado.Cliente);
            Assert.Equal(123, encontrado.Cliente.Codigo);
            Assert.NotNull(encontrado.Usuario);
            Assert.Equal("Juan Perez", encontrado.Usuario.NombreCompleto);

            var inexistente = await uow.Pesajes.ObtenerParaBoletaPorIdAsync(99999);
            Assert.Null(inexistente);
        }
    }

    [Fact]
    public async Task ObtenerParaBoletaPorNumeroAsync_ConNumeroExistenteEInexistente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(7777)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            uow.Pesajes.Agregar(pesaje);
            await uow.GuardarCambiosAsync();
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var encontrado = await uow.Pesajes.ObtenerParaBoletaPorNumeroAsync(7777);

            Assert.NotNull(encontrado);
            Assert.Equal(7777, encontrado.NumeroBoleta);
            Assert.NotNull(encontrado.Ganadero);
            Assert.Equal("AH", encontrado.Ganadero.Marca);
            Assert.NotNull(encontrado.Cliente);
            Assert.Equal(123, encontrado.Cliente.Codigo);
            Assert.NotNull(encontrado.Usuario);
            Assert.Equal("Juan Perez", encontrado.Usuario.NombreCompleto);

            var inexistente = await uow.Pesajes.ObtenerParaBoletaPorNumeroAsync(999999);
            Assert.Null(inexistente);
        }
    }

    [Fact]
    public async Task ContarImpresionesPesajeAsync_FiltraCorrectamentePorEntidadYAcciones()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            // 2 auditorías Imprimir / Reimprimir del pesaje 5
            uow.Auditoria.Registrar(new Domain.Entities.Auditoria
            {
                FechaHora = DateTime.UtcNow,
                Accion = AccionAuditoria.Imprimir,
                Entidad = EntidadesAuditoria.Pesaje,
                EntidadId = 5.ToString(CultureInfo.InvariantCulture),
                Motivo = "Impresión 1"
            });
            uow.Auditoria.Registrar(new Domain.Entities.Auditoria
            {
                FechaHora = DateTime.UtcNow,
                Accion = AccionAuditoria.Reimprimir,
                Entidad = EntidadesAuditoria.Pesaje,
                EntidadId = 5.ToString(CultureInfo.InvariantCulture),
                Motivo = "Reimpresión 1"
            });

            // 1 auditoría Crear del pesaje 5 (no debe contar)
            uow.Auditoria.Registrar(new Domain.Entities.Auditoria
            {
                FechaHora = DateTime.UtcNow,
                Accion = AccionAuditoria.Crear,
                Entidad = EntidadesAuditoria.Pesaje,
                EntidadId = 5.ToString(CultureInfo.InvariantCulture),
                Motivo = "Alta de pesaje"
            });

            // 1 auditoría Imprimir del pesaje 6 (no debe contar para pesaje 5)
            uow.Auditoria.Registrar(new Domain.Entities.Auditoria
            {
                FechaHora = DateTime.UtcNow,
                Accion = AccionAuditoria.Imprimir,
                Entidad = EntidadesAuditoria.Pesaje,
                EntidadId = 6.ToString(CultureInfo.InvariantCulture),
                Motivo = "Impresión pesaje 6"
            });

            // 1 auditoría Imprimir con Entidad = "Usuario" y EntidadId = "5" (no debe contar)
            uow.Auditoria.Registrar(new Domain.Entities.Auditoria
            {
                FechaHora = DateTime.UtcNow,
                Accion = AccionAuditoria.Imprimir,
                Entidad = EntidadesAuditoria.Usuario,
                EntidadId = 5.ToString(CultureInfo.InvariantCulture),
                Motivo = "Impresión de usuario"
            });

            await uow.GuardarCambiosAsync();
        }

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var conteoPesaje5 = await uow.Auditoria.ContarImpresionesPesajeAsync(5);
            Assert.Equal(2, conteoPesaje5);

            var conteoSinImpresiones = await uow.Auditoria.ContarImpresionesPesajeAsync(999);
            Assert.Equal(0, conteoSinImpresiones);
        }
    }
}
