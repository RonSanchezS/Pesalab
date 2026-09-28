using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;

namespace PesajeFaeneo.Tests.Utilidades;

public static class SesionesDePrueba
{
    public static Task<UsuarioSesion> IniciarAsync(
        BaseDatosEnMemoria db,
        ISesionActual sesion,
        Rol rol,
        string nombreUsuario = "usuario_test")
    {
        return IniciarAsync(db.ContextFactory, sesion, rol, nombreUsuario);
    }

    public static async Task<UsuarioSesion> IniciarAsync(
        IDbContextFactory<PesajeDbContext> contextFactory,
        ISesionActual sesion,
        Rol rol,
        string nombreUsuario = "usuario_test")
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var usuario = new UsuarioBuilder()
            .ConNombreUsuario(nombreUsuario)
            .ConRol(rol)
            .Build();
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var sesionUsuario = new UsuarioSesion(usuario.Id, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Rol, usuario.DebeCambiarPassword);
        sesion.Iniciar(sesionUsuario);
        return sesionUsuario;
    }
}
