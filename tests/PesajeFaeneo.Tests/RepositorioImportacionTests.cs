using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using Xunit;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Tests;

public class RepositorioImportacionTests
{
    private sealed class TestDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public TestDbContextFactory(DbContextOptions<PesajeDbContext> options) => _options = options;

        public PesajeDbContext CreateDbContext() => new(_options);

        public Task<PesajeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PesajeDbContext(_options));
    }

    private static async Task<(SqliteConnection Connection, IDbContextFactory<PesajeDbContext> Factory)> CrearDbAsync()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var builder = new DbContextOptionsBuilder<PesajeDbContext>();
        builder.ConfigurarSqlite(conn);
        var factory = new TestDbContextFactory(builder.Options);

        var initializer = new DatabaseInitializer(factory);
        await initializer.InicializarAsync();

        var seeder = new DataSeeder(factory, new Pbkdf2PasswordHasher(), TimeProvider.System);
        await seeder.SembrarAsync();

        return (conn, factory);
    }

    [Fact]
    public async Task Test01_ObtenerClavesAsync_DevuelveDiccionariosExactos()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);

            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var g1 = new Ganadero { Marca = "AB", Nombre = "GANADERO AB", Activo = true, CreadoEn = DateTime.UtcNow };
                var g2 = new Ganadero { Marca = "cd", Nombre = "GANADERO CD", Activo = true, CreadoEn = DateTime.UtcNow };
                ctx.Ganaderos.AddRange(g1, g2);

                var c1 = new Cliente { Codigo = 10, Nombre = "CLIENTE 10", Direccion = "CALLE 1", Activo = true, CreadoEn = DateTime.UtcNow };
                ctx.Clientes.Add(c1);

                var admin = await ctx.Usuarios.FirstAsync();
                await ctx.SaveChangesAsync();

                var p1 = new Pesaje
                {
                    NumeroBoleta = 100,
                    FechaHora = DateTime.UtcNow,
                    GanaderoId = g1.Id,
                    ClienteId = c1.Id,
                    Direccion = "CALLE 1",
                    Secuencia = 1,
                    Sexo = 'M',
                    TipoDesposte = TipoDesposte.MediaRes,
                    PesoIzquierdo = 120.5m,
                    PesoDerecho = 0,
                    PesoTotal = 120.5m,
                    OrigenPeso = OrigenPeso.Balanza,
                    UsuarioId = admin.Id,
                    CreadoEn = DateTime.UtcNow,
                    Version = 1
                };

                var p2 = new Pesaje
                {
                    NumeroBoleta = 200,
                    FechaHora = DateTime.UtcNow,
                    GanaderoId = g2.Id,
                    ClienteId = c1.Id,
                    Direccion = "CALLE 1",
                    Secuencia = 1,
                    Sexo = 'H',
                    TipoDesposte = TipoDesposte.MediaRes,
                    PesoIzquierdo = 110.0m,
                    PesoDerecho = 0,
                    PesoTotal = 110.0m,
                    OrigenPeso = OrigenPeso.Importado,
                    UsuarioId = admin.Id,
                    CreadoEn = DateTime.UtcNow,
                    Version = 1
                };

                ctx.Pesajes.AddRange(p1, p2);
                await ctx.SaveChangesAsync();
            }

            var claves = await repo.ObtenerClavesAsync();

            Assert.Equal(2, claves.GanaderosPorMarca.Count);
            Assert.True(claves.GanaderosPorMarca.ContainsKey("AB"));
            Assert.True(claves.GanaderosPorMarca.ContainsKey("CD"));

            Assert.Single(claves.ClientesPorCodigo);
            Assert.True(claves.ClientesPorCodigo.ContainsKey(10));
            Assert.Equal("CALLE 1", claves.ClientesPorCodigo[10].Direccion);

            Assert.Equal(2, claves.BoletasExistentes.Count);
            Assert.Equal(OrigenPeso.Balanza, claves.BoletasExistentes[100]);
            Assert.Equal(OrigenPeso.Importado, claves.BoletasExistentes[200]);
        }
    }

    [Fact]
    public async Task Test02_Ganaderos_ConfirmarTrueGuarda_ConfirmarFalseRevierte()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);
            var ahoraUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            var plan = new PlanImportacion(
                TipoImportacion.Ganaderos,
                FilasLeidas: 2,
                Ganaderos: new List<GanaderoAImportar>
                {
                    new(1, "G1", "GANADERO UNO", true),
                    new(2, "G2", "GANADERO DOS", true)
                },
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: Array.Empty<PesajeAImportar>(),
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            // Simulacro: confirmar = false
            var insertadasSim = await repo.EscribirAsync(plan, usuarioId: 1, ahoraUtc, auditoria: null, confirmar: false);
            Assert.Equal(2, insertadasSim);

            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var count = await ctx.Ganaderos.CountAsync();
                Assert.Equal(0, count);
            }

            // Real: confirmar = true
            var insertadasReal = await repo.EscribirAsync(plan, usuarioId: 1, ahoraUtc, auditoria: null, confirmar: true);
            Assert.Equal(2, insertadasReal);

            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var ganaderos = await ctx.Ganaderos.OrderBy(g => g.Marca).ToListAsync();
                Assert.Equal(2, ganaderos.Count);
                Assert.Equal("G1", ganaderos[0].Marca);
                Assert.Equal(ahoraUtc, ganaderos[0].CreadoEn);
                Assert.Equal("G2", ganaderos[1].Marca);
            }
        }
    }

    [Fact]
    public async Task Test03_Pesajes_InsertaCamposYSiguienteNumeroBoleta()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);
            var ahoraUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

            int ganaderoId;
            int clienteId;
            int adminId;
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var g = new Ganadero { Marca = "GX", Nombre = "GANADERO GX", Activo = true, CreadoEn = ahoraUtc };
                var c = new Cliente { Codigo = 50, Nombre = "CLIENTE 50", Direccion = "AV CENTRAL", Activo = true, CreadoEn = ahoraUtc };
                ctx.Ganaderos.Add(g);
                ctx.Clientes.Add(c);
                var admin = await ctx.Usuarios.FirstAsync();
                await ctx.SaveChangesAsync();
                ganaderoId = g.Id;
                clienteId = c.Id;
                adminId = admin.Id;
            }

            var plan = new PlanImportacion(
                TipoImportacion.Pesajes,
                FilasLeidas: 2,
                Ganaderos: Array.Empty<GanaderoAImportar>(),
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: new List<PesajeAImportar>
                {
                    new(1, 300, ahoraUtc.AddHours(-2), ganaderoId, clienteId, "AV CENTRAL", 1, "NEGRO", 'M',
                        TipoDesposte.MediaRes, 120.0m, 0, 120.0m, "OBS 1", EstadoPesaje.Vigente, null),
                    new(2, 400, ahoraUtc.AddHours(-1), ganaderoId, clienteId, "AV CENTRAL", 2, "BLANCO", 'H',
                        TipoDesposte.ResCompleta, 110.0m, 105.0m, 215.0m, "OBS 2", EstadoPesaje.Anulado, "MOTIVO ERROR")
                },
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            // Con confirmar: false no cambia
            await repo.EscribirAsync(plan, adminId, ahoraUtc, auditoria: null, confirmar: false);
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                Assert.Equal(0, await ctx.Pesajes.CountAsync());
                var param = await ctx.ParametrosSistema.FirstAsync();
                Assert.Equal(1, param.SiguienteNumeroBoleta);
            }

            // Con confirmar: true
            await repo.EscribirAsync(plan, adminId, ahoraUtc, auditoria: null, confirmar: true);
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var pesajes = await ctx.Pesajes.OrderBy(p => p.NumeroBoleta).ToListAsync();
                Assert.Equal(2, pesajes.Count);

                var p1 = pesajes[0];
                Assert.Equal(300, p1.NumeroBoleta);
                Assert.Equal(OrigenPeso.Importado, p1.OrigenPeso);
                Assert.Equal(adminId, p1.UsuarioId);
                Assert.Equal(EstadoPesaje.Vigente, p1.Estado);
                Assert.Null(p1.AnuladoPorId);

                var p2 = pesajes[1];
                Assert.Equal(400, p2.NumeroBoleta);
                Assert.Equal(EstadoPesaje.Anulado, p2.Estado);
                Assert.Equal(adminId, p2.AnuladoPorId);
                Assert.Equal(p2.FechaHora, p2.AnuladoEn);
                Assert.Equal("MOTIVO ERROR", p2.MotivoAnulacion);

                var param = await ctx.ParametrosSistema.FirstAsync();
                Assert.Equal(401, param.SiguienteNumeroBoleta);
            }

            // Si SiguienteNumeroBoleta ya era mayor (9000), se conserva
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync("UPDATE ParametrosSistema SET SiguienteNumeroBoleta = 9000 WHERE Id = 1");
            }

            var plan2 = new PlanImportacion(
                TipoImportacion.Pesajes,
                FilasLeidas: 1,
                Ganaderos: Array.Empty<GanaderoAImportar>(),
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: new List<PesajeAImportar>
                {
                    new(1, 500, ahoraUtc, ganaderoId, clienteId, "AV CENTRAL", 3, "ROJO", 'M',
                        TipoDesposte.MediaRes, 115.0m, 0, 115.0m, "", EstadoPesaje.Vigente, null)
                },
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            await repo.EscribirAsync(plan2, adminId, ahoraUtc, auditoria: null, confirmar: true);
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var param = await ctx.ParametrosSistema.FirstAsync();
                Assert.Equal(9000, param.SiguienteNumeroBoleta);
            }
        }
    }

    [Fact]
    public async Task Test04_Auditoria_SoloQuedaConConfirmarTrue()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);
            var ahoraUtc = DateTime.UtcNow;

            var plan = new PlanImportacion(
                TipoImportacion.Ganaderos,
                FilasLeidas: 1,
                Ganaderos: new List<GanaderoAImportar> { new(1, "GA", "GANADERO A", true) },
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: Array.Empty<PesajeAImportar>(),
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            var aud = new AuditoriaEntidad
            {
                FechaHora = ahoraUtc,
                UsuarioId = 1,
                Accion = AccionAuditoria.Importacion,
                Entidad = "Sistema",
                EntidadId = "Ganaderos"
            };

            await repo.EscribirAsync(plan, usuarioId: 1, ahoraUtc, auditoria: aud, confirmar: false);
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                Assert.Equal(0, await ctx.Auditorias.CountAsync());
            }

            var aud2 = new AuditoriaEntidad
            {
                FechaHora = ahoraUtc,
                UsuarioId = 1,
                Accion = AccionAuditoria.Importacion,
                Entidad = "Sistema",
                EntidadId = "Ganaderos"
            };

            await repo.EscribirAsync(plan, usuarioId: 1, ahoraUtc, auditoria: aud2, confirmar: true);
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                Assert.Equal(1, await ctx.Auditorias.CountAsync());
            }
        }
    }

    [Fact]
    public async Task Test05_2500Pesajes_SeInsertanEnLotesCorrectamente()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);
            var ahoraUtc = DateTime.UtcNow;

            int ganaderoId;
            int clienteId;
            int adminId;
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var g = new Ganadero { Marca = "GL", Nombre = "GANADERO LOTES", Activo = true, CreadoEn = ahoraUtc };
                var c = new Cliente { Codigo = 99, Nombre = "CLIENTE LOTES", Direccion = "ZONA SUR", Activo = true, CreadoEn = ahoraUtc };
                ctx.Ganaderos.Add(g);
                ctx.Clientes.Add(c);
                var admin = await ctx.Usuarios.FirstAsync();
                await ctx.SaveChangesAsync();
                ganaderoId = g.Id;
                clienteId = c.Id;
                adminId = admin.Id;
            }

            var pesajes = new List<PesajeAImportar>(2500);
            for (int i = 1; i <= 2500; i++)
            {
                pesajes.Add(new PesajeAImportar(
                    i,
                    10000 + i,
                    ahoraUtc,
                    ganaderoId,
                    clienteId,
                    "ZONA SUR",
                    1,
                    "",
                    'M',
                    TipoDesposte.MediaRes,
                    100.0m,
                    0,
                    100.0m,
                    "",
                    EstadoPesaje.Vigente,
                    null));
            }

            var plan = new PlanImportacion(
                TipoImportacion.Pesajes,
                FilasLeidas: 2500,
                Ganaderos: Array.Empty<GanaderoAImportar>(),
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: pesajes,
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            var insertadas = await repo.EscribirAsync(plan, adminId, ahoraUtc, auditoria: null, confirmar: true);
            Assert.Equal(2500, insertadas);

            await using (var ctx = await factory.CreateDbContextAsync())
            {
                Assert.Equal(2500, await ctx.Pesajes.CountAsync());
            }
        }
    }

    [Fact]
    public async Task Test06_SecuenciaInvalida_LanzaReglaNegocioExceptionIMP_BD_YRevierteTodo()
    {
        var (conn, factory) = await CrearDbAsync();
        await using (conn)
        {
            var repo = new RepositorioImportacion(factory, NullLogger<RepositorioImportacion>.Instance);
            var ahoraUtc = DateTime.UtcNow;

            int ganaderoId;
            int clienteId;
            int adminId;
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var g = new Ganadero { Marca = "GERR", Nombre = "GANADERO ERROR", Activo = true, CreadoEn = ahoraUtc };
                var c = new Cliente { Codigo = 77, Nombre = "CLIENTE ERROR", Direccion = "NORTE", Activo = true, CreadoEn = ahoraUtc };
                ctx.Ganaderos.Add(g);
                ctx.Clientes.Add(c);
                var admin = await ctx.Usuarios.FirstAsync();
                await ctx.SaveChangesAsync();
                ganaderoId = g.Id;
                clienteId = c.Id;
                adminId = admin.Id;
            }

            // Plan con ganadero nuevo y pesaje con Secuencia = 0 (viola CHECK constraint en Pesajes)
            var plan = new PlanImportacion(
                TipoImportacion.Pesajes,
                FilasLeidas: 1,
                Ganaderos: new List<GanaderoAImportar> { new(1, "GNEW", "NUEVO", true) },
                Clientes: Array.Empty<ClienteAImportar>(),
                Pesajes: new List<PesajeAImportar>
                {
                    new(1, 99999, ahoraUtc, ganaderoId, clienteId, "NORTE", 0, "", 'M',
                        TipoDesposte.MediaRes, 100.0m, 0, 100.0m, "", EstadoPesaje.Vigente, null)
                },
                Incidencias: Array.Empty<Incidencia>(),
                IncidenciasTruncadas: false,
                FilasConError: 0,
                FilasOmitidas: 0,
                FilasConAdvertencia: 0);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                repo.EscribirAsync(plan, adminId, ahoraUtc, auditoria: null, confirmar: true));

            Assert.Equal("IMP_BD", ex.Codigo);

            await using (var ctx = await factory.CreateDbContextAsync())
            {
                Assert.Null(await ctx.Ganaderos.FirstOrDefaultAsync(g => g.Marca == "GNEW"));
                Assert.Equal(0, await ctx.Pesajes.CountAsync());
            }
        }
    }
}
