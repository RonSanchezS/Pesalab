using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class PersistenciaIntegracionTests : IClassFixture<SqliteDbFixture>
{
    private readonly SqliteDbFixture _fixture;

    public PersistenciaIntegracionTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Ganadero_MarcaDuplicada_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var g1 = new GanaderoBuilder().ConMarca("AH").ConNombre("Ganadero 1").Build();
        var g2 = new GanaderoBuilder().ConMarca("AH").ConNombre("Ganadero 2").Build();

        ctx.Ganaderos.Add(g1);
        await ctx.SaveChangesAsync();

        ctx.Ganaderos.Add(g2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Cliente_CodigoDuplicado_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var c1 = new ClienteBuilder().ConCodigo(999).ConNombre("Cliente 1").Build();
        var c2 = new ClienteBuilder().ConCodigo(999).ConNombre("Cliente 2").Build();

        ctx.Clientes.Add(c1);
        await ctx.SaveChangesAsync();

        ctx.Clientes.Add(c2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fletero_OrdenFueraDeRango_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var fleteroInvalido = new Fletero
        {
            Orden = 5, // Inválido: CHECK es BETWEEN 1 AND 4
            Nombre = "Fletero Inválido",
            Celular = "77712345",
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        ctx.Fleteros.Add(fleteroInvalido);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fletero_IndiceFiltrado_PermiteDosInactivosConMismoOrdenPeroNoDosActivos()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var fActivo1 = new Fletero { Orden = 2, Nombre = "F1", Celular = "70000001", Activo = true, CreadoEn = DateTime.UtcNow };
        var fInactivo1 = new Fletero { Orden = 2, Nombre = "F2", Celular = "70000002", Activo = false, CreadoEn = DateTime.UtcNow };
        var fActivo2 = new Fletero { Orden = 2, Nombre = "F3", Celular = "70000003", Activo = true, CreadoEn = DateTime.UtcNow };

        ctx.Fleteros.AddRange(fActivo1, fInactivo1);
        await ctx.SaveChangesAsync(); // Debe permitir activo + inactivo con mismo orden

        ctx.Fleteros.Add(fActivo2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync()); // Debe fallar segundo activo con orden 2
    }

    [Fact]
    public async Task Pesaje_PesoDecimal_AlmacenadoComoReal_RecuperaValorExacto()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var ganadero = new GanaderoBuilder().ConMarca("ZZ").ConNombre("Ganadero ZZ").Build();
        var cliente = new ClienteBuilder().ConCodigo(888).ConNombre("Cliente 888").Build();
        var usuario = new UsuarioBuilder().ConNombreUsuario("pesador1").Build();

        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(99991)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConPesos(120.5m, 0.0m)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await ctx.SaveChangesAsync();

        // Limpiar seguimiento y releer
        ctx.ChangeTracker.Clear();

        var releido = await ctx.Pesajes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pesaje.Id);
        Assert.NotNull(releido);
        Assert.Equal(120.5m, releido.PesoIzquierdo);
        Assert.Equal(0.0m, releido.PesoDerecho);
        Assert.Equal(120.5m, releido.PesoTotal);
    }

    [Fact]
    public async Task ParametrosSistema_SegundaFilaConIdDistinto_LanzaDbUpdateException()
    {
        // CK_ParametrosSistema_Id fuerza Id = 1: es una tabla singleton (un único registro de parámetros).
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        ctx.ParametrosSistema.Add(new ParametrosSistema
        {
            Id = 2,
            NombreEmpresa = "OTRA EMPRESA",
            ObservacionPorDefecto = "SIN NOVEDAD",
            ModificadoEn = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_SexoInvalido_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PS", 71001, "pesador_sexo");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71001)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.Sexo = 'X'; // Inválido: CK exige 'M' o 'H'

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_TipoDesposteInvalido_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PT", 71002, "pesador_desposte");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71002)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.TipoDesposte = (TipoDesposte)3; // Inválido: CK exige 1 o 2

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_EstadoInvalido_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PE", 71003, "pesador_estado");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71003)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.Estado = (EstadoPesaje)3; // Inválido: CK exige 1 o 2

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_AnuladoSinMotivoNiAutor_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PA", 71004, "pesador_anulacion");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71004)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        // Estado = Anulado pero sin MotivoAnulacion/AnuladoPorId/AnuladoEn: viola CK_Pesajes_Anulacion.
        pesaje.Estado = EstadoPesaje.Anulado;

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_AnuladoConMotivoYAutor_ConElBuilderCorregido_SeGuardaCorrectamente()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PB", 71005, "pesador_anulacion_ok");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71005)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Anulado("Error de digitación", usuario.Id)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var releido = await ctx.Pesajes.AsNoTracking().SingleAsync(p => p.Id == pesaje.Id);
        Assert.Equal(EstadoPesaje.Anulado, releido.Estado);
        Assert.Equal("Error de digitación", releido.MotivoAnulacion);
        Assert.Equal(usuario.Id, releido.AnuladoPorId);
        Assert.NotNull(releido.AnuladoEn);
    }

    [Fact]
    public async Task Pesaje_PesoNegativo_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("PN", 71006, "pesador_peso_negativo");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71006)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConPesos(-1.0m, 0.0m)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Usuario_RolInvalido_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var usuario = new UsuarioBuilder().ConNombreUsuario("usuario_rol_invalido").Build();
        usuario.Rol = (Rol)9; // Inválido: CK exige 1, 2 o 3

        ctx.Usuarios.Add(usuario);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Usuario_NombreUsuarioDuplicadoPorMayusculas_LanzaDbUpdateException()
    {
        // UseCollation("NOCASE") en NombreUsuario: "admin" y "Admin" deben tratarse como el mismo valor
        // para el índice único (si no, ambos podrían coexistir y el login sería sensible a mayúsculas).
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("colision_login").Build());
        await ctx.SaveChangesAsync();

        ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("COLISION_login").Build());
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Ganadero_MarcaDuplicadaPorMayusculas_LanzaDbUpdateException()
    {
        // UseCollation("NOCASE") en Marca: "ah" y "AH" deben tratarse como la misma marca.
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        ctx.Ganaderos.Add(new GanaderoBuilder().ConMarca("QW").ConNombre("Ganadero QW").Build());
        await ctx.SaveChangesAsync();

        ctx.Ganaderos.Add(new GanaderoBuilder().ConMarca("qw").ConNombre("Ganadero qw minúscula").Build());
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task DateTime_IdaYVuelta_ConservaKindUtcYElInstante()
    {
        var fechaOriginal = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc);

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConMarca("DT").ConNombre("Ganadero DT").ConFechaHora(fechaOriginal).Build();

        ctx.Ganaderos.Add(ganadero);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var releido = await ctx.Ganaderos.AsNoTracking().SingleAsync(g => g.Id == ganadero.Id);

        Assert.Equal(DateTimeKind.Utc, releido.CreadoEn.Kind);
        Assert.Equal(fechaOriginal, releido.CreadoEn);
    }

    private async Task<(Ganadero Ganadero, Cliente Cliente, Usuario Usuario)> CrearCatalogosBaseAsync(
        string marcaGanadero,
        int codigoCliente,
        string nombreUsuario)
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var ganadero = new GanaderoBuilder().ConMarca(marcaGanadero).ConNombre($"Ganadero {marcaGanadero}").Build();
        var cliente = new ClienteBuilder().ConCodigo(codigoCliente).ConNombre($"Cliente {codigoCliente}").Build();
        var usuario = new UsuarioBuilder().ConNombreUsuario(nombreUsuario).Build();

        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        return (ganadero, cliente, usuario);
    }
}
