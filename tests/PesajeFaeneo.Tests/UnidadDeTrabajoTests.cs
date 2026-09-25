using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class UnidadDeTrabajoTests
{
    [Fact]
    public async Task GuardarCambiosAsync_PersisteUsuarioYAuditoriaEnMismaUnidad()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var tiempo = TimeProvider.System;

        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var usuario = new UsuarioBuilder().ConNombreUsuario("operador_uow").Build();
            uow.Usuarios.Agregar(usuario);

            var auditoria = FabricaAuditoria.Crear(
                tiempo,
                usuarioId: null,
                AccionAuditoria.Crear,
                EntidadesAuditoria.Usuario,
                "operador_uow",
                antes: null,
                despues: usuario,
                motivo: "Alta de prueba");

            uow.Auditoria.Registrar(auditoria);
            await uow.GuardarCambiosAsync();
        }

        // Verificamos con un nuevo contexto limpio
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var usr = await context.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == "operador_uow");
        Assert.NotNull(usr);

        var aud = await context.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == "operador_uow");
        Assert.NotNull(aud);
        Assert.Equal(AccionAuditoria.Crear, aud.Accion);
    }

    [Fact]
    public async Task GuardarCambiosAsync_SiFallaOperacion_NoPersisteNingunaEntidad_GarantizaAtomicidad()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var tiempo = TimeProvider.System;

        // Primero creamos un usuario existente
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var uExistente = new UsuarioBuilder().ConNombreUsuario("existente").Build();
            uow.Usuarios.Agregar(uExistente);
            await uow.GuardarCambiosAsync();
        }

        // Ahora intentamos agregar en una misma uow un usuario duplicado y una auditoría
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            var uDuplicado = new UsuarioBuilder().ConNombreUsuario("existente").Build();
            uow.Usuarios.Agregar(uDuplicado);

            var auditoria = FabricaAuditoria.Crear(
                tiempo,
                usuarioId: null,
                AccionAuditoria.Crear,
                EntidadesAuditoria.Usuario,
                "existente_duplicado",
                antes: null,
                despues: null,
                motivo: "Debe revertirse");

            uow.Auditoria.Registrar(auditoria);

            // Debe lanzar ConflictoException
            await Assert.ThrowsAsync<ConflictoException>(() => uow.GuardarCambiosAsync());
        }

        // Verificamos que la auditoría "existente_duplicado" NO fue persistida
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var aud = await context.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == "existente_duplicado");
        Assert.Null(aud);
    }

    [Fact]
    public async Task GuardarCambiosAsync_UsuarioDuplicado_LanzaConflictoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using (var uow1 = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            uow1.Usuarios.Agregar(new UsuarioBuilder().ConNombreUsuario("juan").Build());
            await uow1.GuardarCambiosAsync();
        }

        await using (var uow2 = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            uow2.Usuarios.Agregar(new UsuarioBuilder().ConNombreUsuario("juan").Build());
            var ex = await Assert.ThrowsAsync<ConflictoException>(() => uow2.GuardarCambiosAsync());
            Assert.Contains("Ya existe un registro con ese valor.", ex.Message);
        }
    }

    [Fact]
    public async Task GuardarCambiosAsync_ViolacionCheckConstraint_LanzaReglaNegocioExceptionConCodigoBD_CHECK()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using var uow = await db.UnidadDeTrabajoFactory.CrearAsync();
        var usuarioInvalido = new UsuarioBuilder()
            .ConNombreUsuario("invalido_rol")
            .ConRol((Rol)9) // CHECK Rol IN (1, 2, 3)
            .Build();

        uow.Usuarios.Agregar(usuarioInvalido);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => uow.GuardarCambiosAsync());
        Assert.Equal("BD_CHECK", ex.Codigo);
        Assert.Contains("Los datos no cumplen una regla de integridad.", ex.Message);
    }

    [Fact]
    public async Task IniciarTransaccionAsync_Confirmada_PersisteEntidadesYAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var tiempo = TimeProvider.System;

        int ganaderoId;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            await using var tx = await uow.IniciarTransaccionAsync();

            var ganadero = new GanaderoBuilder().ConMarca("TX").ConNombre("Ganadero TX").Build();
            uow.Ganaderos.Agregar(ganadero);
            await uow.GuardarCambiosAsync();

            ganaderoId = ganadero.Id;
            Assert.True(ganaderoId > 0);

            var auditoria = FabricaAuditoria.Crear(
                tiempo,
                usuarioId: null,
                AccionAuditoria.Crear,
                EntidadesAuditoria.Ganadero,
                ganaderoId.ToString(),
                despues: FabricaAuditoria.CrearSnapshot(ganadero),
                motivo: "Alta de ganadero con tx");

            uow.Auditoria.Registrar(auditoria);
            await uow.GuardarCambiosAsync();

            await tx.ConfirmarAsync();
        }

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = await ctx.Ganaderos.FindAsync(ganaderoId);
        Assert.NotNull(g);
        var a = await ctx.Auditorias.FirstOrDefaultAsync(x => x.EntidadId == ganaderoId.ToString() && x.Entidad == EntidadesAuditoria.Ganadero);
        Assert.NotNull(a);
    }

    [Fact]
    public async Task IniciarTransaccionAsync_NoConfirmada_RevierteTodo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int ganaderoId = 0;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            await using (var tx = await uow.IniciarTransaccionAsync())
            {
                var ganadero = new GanaderoBuilder().ConMarca("NO").ConNombre("Ganadero No Confirmado").Build();
                uow.Ganaderos.Agregar(ganadero);
                await uow.GuardarCambiosAsync();
                ganaderoId = ganadero.Id;

                // No llamamos a tx.ConfirmarAsync()
            }
        }

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = await ctx.Ganaderos.FindAsync(ganaderoId);
        Assert.Null(g);
    }

    [Fact]
    public async Task IniciarTransaccionAsync_SegundoGuardarFallaPorFk_LanzaReglaNegocioExceptionBD_FK_YRevierte()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var tiempo = TimeProvider.System;

        int ganaderoId = 0;
        await using (var uow = await db.UnidadDeTrabajoFactory.CrearAsync())
        {
            await using var tx = await uow.IniciarTransaccionAsync();

            var ganadero = new GanaderoBuilder().ConMarca("FK").ConNombre("Ganadero FK").Build();
            uow.Ganaderos.Agregar(ganadero);
            await uow.GuardarCambiosAsync();
            ganaderoId = ganadero.Id;

            // Registrar auditoría con UsuarioId inexistente (viola FK con Usuarios)
            var auditoriaInvalida = FabricaAuditoria.Crear(
                tiempo,
                usuarioId: 9999,
                AccionAuditoria.Crear,
                EntidadesAuditoria.Ganadero,
                ganaderoId.ToString(),
                motivo: "Auditoria con FK invalida");

            uow.Auditoria.Registrar(auditoriaInvalida);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => uow.GuardarCambiosAsync());
            Assert.Equal("BD_FK", ex.Codigo);
            Assert.Contains("El registro está relacionado con otros datos", ex.Message);
        }

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = await ctx.Ganaderos.FindAsync(ganaderoId);
        Assert.Null(g);
    }

    [Fact]
    public async Task IniciarTransaccionAsync_DosVecesEnMismaUnidad_LanzaInvalidOperationException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var uow = await db.UnidadDeTrabajoFactory.CrearAsync();

        await using var tx1 = await uow.IniciarTransaccionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.IniciarTransaccionAsync());
    }
}
