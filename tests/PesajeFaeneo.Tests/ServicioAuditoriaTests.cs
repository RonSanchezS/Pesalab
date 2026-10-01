using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Tests;

public class ServicioAuditoriaTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Audit_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private static (ServicioAuditoria Servicio, SesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        DateTime? ahoraUtc = null)
    {
        var tiempo = new FakeTimeProvider(ahoraUtc ?? new DateTime(2026, 9, 27, 14, 0, 0, DateTimeKind.Utc));
        tiempo.SetLocalTimeZone(ZonaBolivia);
        var sesion = new SesionActual();
        var servicio = new ServicioAuditoria(
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            NullLogger<ServicioAuditoria>.Instance);

        return (servicio, sesion, tiempo);
    }

    [Fact]
    public async Task ConsultarAsync_SinSesionOOOperador_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        // Sin sesión
        await Assert.ThrowsAsync<NoAutorizadoException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null)));

        // Operador (no tiene VerAuditoria)
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador, "user_op");
        await Assert.ThrowsAsync<NoAutorizadoException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null)));

        // Supervisor y Administrador sí tienen VerAuditoria
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor, "user_sup");
        var resSup = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null));
        Assert.NotNull(resSup);

        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador, "user_adm");
        var resAdmin = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null));
        Assert.NotNull(resAdmin);
    }

    [Fact]
    public async Task ListarUsuariosAsync_SinSesionOOOperador_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarUsuariosAsync());

        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarUsuariosAsync());
    }

    [Fact]
    public async Task ListarUsuariosAsync_IncluyeInactivos_OrdenadosPorNombreUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Usuarios.AddRange(
                new UsuarioBuilder().ConNombreUsuario("carlos").ConNombreCompleto("Carlos B").ConRol(Rol.Operador).Build(),
                new UsuarioBuilder().ConNombreUsuario("ana").ConNombreCompleto("Ana A").ConRol(Rol.Supervisor).Inactivo().Build(),
                new UsuarioBuilder().ConNombreUsuario("beto").ConNombreCompleto("Beto C").ConRol(Rol.Operador).Build()
            );
            await ctx.SaveChangesAsync();
        }

        var lista = await servicio.ListarUsuariosAsync();

        // Viene admin (de la semilla) + ana + beto + carlos
        Assert.True(lista.Count >= 4);
        var nombres = lista.Select(u => u.NombreUsuario).ToList();
        var ordenado = nombres.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(ordenado, nombres);

        var ana = Assert.Single(lista, u => u.NombreUsuario == "ana");
        Assert.Equal("Ana A", ana.NombreCompleto);
    }

    [Fact]
    public async Task ConsultarAsync_Validaciones_DesdeMayorQueHasta_YNumeroBoletaInvalido()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var ex1 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(
                new DateOnly(2026, 9, 28),
                new DateOnly(2026, 9, 27),
                null, null, null, null)));
        Assert.Contains(ex1.Errores, e => e.Campo == CamposGestionPesajes.FechaHasta);

        var ex2 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(
                null, null, null, null, null, 0)));
        Assert.Contains(ex2.Errores, e => e.Campo == CamposGestionPesajes.NumeroBoleta);

        var ex3 = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(
                null, null, null, null, null, -5)));
        Assert.Contains(ex3.Errores, e => e.Campo == CamposGestionPesajes.NumeroBoleta);
    }

    [Fact]
    public async Task ConsultarAsync_FiltrosMultiples_AND_PorFechaEntidadAccionYUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var admin = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        int user2Id;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var u2 = new UsuarioBuilder().ConNombreUsuario("oper1").ConNombreCompleto("Operador 1").ConRol(Rol.Operador).Build();
            ctx.Usuarios.Add(u2);
            await ctx.SaveChangesAsync();
            user2Id = u2.Id;

            // Rango de fechas: día local 2026-09-27 en UTC-4 es 2026-09-27 04:00 UTC a 2026-09-28 04:00 UTC
            ctx.Auditorias.AddRange(
                new AuditoriaEntidad
                {
                    FechaHora = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc), // Dentro de 27/09
                    UsuarioId = admin.Id,
                    Accion = AccionAuditoria.Modificar,
                    Entidad = EntidadesAuditoria.Cliente,
                    EntidadId = "10"
                },
                new AuditoriaEntidad
                {
                    FechaHora = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc), // Coincide todo
                    UsuarioId = admin.Id,
                    Accion = AccionAuditoria.Crear,
                    Entidad = EntidadesAuditoria.Ganadero,
                    EntidadId = "1"
                },
                new AuditoriaEntidad
                {
                    FechaHora = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc), // Distinto usuario
                    UsuarioId = user2Id,
                    Accion = AccionAuditoria.Crear,
                    Entidad = EntidadesAuditoria.Ganadero,
                    EntidadId = "2"
                },
                new AuditoriaEntidad
                {
                    FechaHora = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), // Fuera de fecha
                    UsuarioId = admin.Id,
                    Accion = AccionAuditoria.Crear,
                    Entidad = EntidadesAuditoria.Ganadero,
                    EntidadId = "3"
                }
            );
            await ctx.SaveChangesAsync();
        }

        var res = await servicio.ConsultarAsync(new FiltroAuditoria(
            Desde: new DateOnly(2026, 9, 27),
            Hasta: new DateOnly(2026, 9, 27),
            Entidad: EntidadesAuditoria.Ganadero,
            Accion: AccionAuditoria.Crear,
            UsuarioId: admin.Id,
            NumeroBoleta: null));

        var reg = Assert.Single(res.Registros);
        Assert.Equal("1", reg.EntidadId);
        Assert.Equal(admin.NombreUsuario, reg.Usuario);
        Assert.Equal(AccionAuditoria.Crear, reg.Accion);
        Assert.Equal(EntidadesAuditoria.Ganadero, reg.Entidad);
    }

    [Fact]
    public async Task ConsultarAsync_FiltroPorNumeroBoleta_DevuelveCicloCompletoDeEsaBoletaYElNumeroGanaSobreEntidad()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int p1Id, p2Id;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var p1 = new PesajeBuilder().ConNumeroBoleta(4001).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(u.Id).Build();
            var p2 = new PesajeBuilder().ConNumeroBoleta(4002).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(u.Id).Build();
            ctx.Pesajes.AddRange(p1, p2);
            await ctx.SaveChangesAsync();
            p1Id = p1.Id;
            p2Id = p2.Id;

            // Auditorías de p1: Crear, Imprimir, Modificar, Anular
            ctx.Auditorias.AddRange(
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow.AddMinutes(-40), UsuarioId = u.Id, Accion = AccionAuditoria.Crear, Entidad = EntidadesAuditoria.Pesaje, EntidadId = p1Id.ToString(CultureInfo.InvariantCulture) },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow.AddMinutes(-30), UsuarioId = u.Id, Accion = AccionAuditoria.Imprimir, Entidad = EntidadesAuditoria.Pesaje, EntidadId = p1Id.ToString(CultureInfo.InvariantCulture) },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow.AddMinutes(-20), UsuarioId = u.Id, Accion = AccionAuditoria.Modificar, Entidad = EntidadesAuditoria.Pesaje, EntidadId = p1Id.ToString(CultureInfo.InvariantCulture) },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow.AddMinutes(-10), UsuarioId = u.Id, Accion = AccionAuditoria.Anular, Entidad = EntidadesAuditoria.Pesaje, EntidadId = p1Id.ToString(CultureInfo.InvariantCulture) },
                // Auditoría de p2
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow.AddMinutes(-5), UsuarioId = u.Id, Accion = AccionAuditoria.Crear, Entidad = EntidadesAuditoria.Pesaje, EntidadId = p2Id.ToString(CultureInfo.InvariantCulture) }
            );
            await ctx.SaveChangesAsync();
        }

        // Aunque se pida Entidad = "Usuario", NumeroBoleta = 4001 GANA
        var res = await servicio.ConsultarAsync(new FiltroAuditoria(
            Desde: null,
            Hasta: null,
            Entidad: EntidadesAuditoria.Usuario,
            Accion: null,
            UsuarioId: null,
            NumeroBoleta: 4001));

        Assert.Equal(4, res.Registros.Count);
        Assert.All(res.Registros, r =>
        {
            Assert.Equal(EntidadesAuditoria.Pesaje, r.Entidad);
            Assert.Equal(p1Id.ToString(CultureInfo.InvariantCulture), r.EntidadId);
        });

        // Boleta inexistente -> ValidacionException
        var ex = await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, 99999)));
        Assert.Contains("99999", ex.Errores.Single().Mensaje);
    }

    [Fact]
    public async Task ConsultarAsync_TruncadoA500_RetornaTruncadoTrueYExactamente500Filas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var lista = new List<AuditoriaEntidad>(501);
            var baseTime = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 501; i++)
            {
                lista.Add(new AuditoriaEntidad
                {
                    FechaHora = baseTime.AddSeconds(i),
                    UsuarioId = u.Id,
                    Accion = AccionAuditoria.Modificar,
                    Entidad = EntidadesAuditoria.Pesaje,
                    EntidadId = (i + 1).ToString(CultureInfo.InvariantCulture)
                });
            }
            ctx.Auditorias.AddRange(lista);
            await ctx.SaveChangesAsync();
        }

        var res = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null));

        Assert.True(res.Truncado);
        Assert.Equal(ReglasGestionPesajes.AuditoriaMaximoFilas, res.Registros.Count);
    }

    [Fact]
    public async Task ConsultarAsync_UsuarioIdNulo_MapeaAUsuarioSistema()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Auditorias.Add(new AuditoriaEntidad
            {
                FechaHora = DateTime.UtcNow,
                UsuarioId = null,
                Accion = AccionAuditoria.LoginFallido,
                Entidad = EntidadesAuditoria.Usuario,
                EntidadId = "(usuario no existente)"
            });
            await ctx.SaveChangesAsync();
        }

        var res = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null));

        var reg = Assert.Single(res.Registros);
        Assert.Equal("(sistema)", reg.Usuario);
    }

    [Fact]
    public async Task ConsultarAsync_D19_LoginFallidoSinUsuario_EnmascaraEntidadId_YConUsuarioConserva()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var admin = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // LoginFallido sin usuario pero con password tipeada como "EntidadId" por accidente o legado
            ctx.Auditorias.Add(new AuditoriaEntidad
            {
                FechaHora = DateTime.UtcNow.AddMinutes(-2),
                UsuarioId = null,
                Accion = AccionAuditoria.LoginFallido,
                Entidad = EntidadesAuditoria.Usuario,
                EntidadId = "miClaveSecreta123"
            });

            // LoginFallido con usuario válido existente
            ctx.Auditorias.Add(new AuditoriaEntidad
            {
                FechaHora = DateTime.UtcNow.AddMinutes(-1),
                UsuarioId = admin.Id,
                Accion = AccionAuditoria.LoginFallido,
                Entidad = EntidadesAuditoria.Usuario,
                EntidadId = admin.Id.ToString(CultureInfo.InvariantCulture)
            });

            await ctx.SaveChangesAsync();
        }

        var res = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, AccionAuditoria.LoginFallido, null, null));

        Assert.Equal(2, res.Registros.Count);

        var sinUsuario = Assert.Single(res.Registros, r => r.Usuario == "(sistema)");
        Assert.Equal("(usuario no existente)", sinUsuario.EntidadId);

        // Asegurar que en ningún campo del registro aparece la clave secreta
        Assert.DoesNotContain("miClaveSecreta123", sinUsuario.EntidadId);
        Assert.DoesNotContain("miClaveSecreta123", sinUsuario.Usuario);
        Assert.DoesNotContain("miClaveSecreta123", sinUsuario.Motivo ?? "");

        var conUsuario = Assert.Single(res.Registros, r => r.Usuario == admin.NombreUsuario);
        Assert.Equal(admin.Id.ToString(CultureInfo.InvariantCulture), conUsuario.EntidadId);
    }

    [Fact]
    public async Task ConsultarAsync_HoraLocalConvertida_YOrdenFechaHoraDescIdDesc()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var u = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var utc1 = new DateTime(2026, 9, 27, 14, 0, 0, DateTimeKind.Utc);
        var utc2 = new DateTime(2026, 9, 27, 15, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Auditorias.AddRange(
                new AuditoriaEntidad { FechaHora = utc1, UsuarioId = u.Id, Accion = AccionAuditoria.Crear, Entidad = EntidadesAuditoria.Pesaje, EntidadId = "1" },
                new AuditoriaEntidad { FechaHora = utc2, UsuarioId = u.Id, Accion = AccionAuditoria.Crear, Entidad = EntidadesAuditoria.Pesaje, EntidadId = "2" }
            );
            await ctx.SaveChangesAsync();
        }

        var res = await servicio.ConsultarAsync(new FiltroAuditoria(null, null, null, null, null, null));

        Assert.Equal(2, res.Registros.Count);

        // Orden desc: utc2 primero, luego utc1
        Assert.Equal("2", res.Registros[0].EntidadId);
        Assert.Equal(new DateTime(2026, 9, 27, 11, 0, 0, DateTimeKind.Unspecified), res.Registros[0].FechaHoraLocal);
        Assert.Equal(DateTimeKind.Unspecified, res.Registros[0].FechaHoraLocal.Kind);

        Assert.Equal("1", res.Registros[1].EntidadId);
        Assert.Equal(new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Unspecified), res.Registros[1].FechaHoraLocal);
        Assert.Equal(DateTimeKind.Unspecified, res.Registros[1].FechaHoraLocal.Kind);
    }
}
