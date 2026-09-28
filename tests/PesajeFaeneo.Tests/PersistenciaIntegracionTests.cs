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
    public async Task Pesaje_SecuenciaMayorA9999_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P1", 71007, "pesador_secuencia_10000");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71007)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConSecuencia(10000)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_NumeroBoletaCero_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P2", 71008, "pesador_boleta_cero");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(0)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_PesoTotalIncoherente_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P3", 71009, "pesador_total_incoherente");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71009)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConPesos(100.0m, 100.0m)
            .Build();
        pesaje.PesoTotal = 150.0m;

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_OrigenPesoInvalido_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P4", 71010, "pesador_origen_invalido");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71010)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConOrigen((OrigenPeso)9)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_DireccionMayorA150Caracteres_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P5", 71011, "pesador_direccion_larga");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71011)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.Direccion = new string('A', 151);

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_ColorRazaMayorA30Caracteres_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P6", 71012, "pesador_color_largo");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71012)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.ColorRaza = new string('B', 31);

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_ObservacionMayorA250Caracteres_LanzaDbUpdateException()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P7", 71013, "pesador_obs_larga");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71013)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .Build();
        pesaje.Observacion = new string('C', 251);

        ctx.Pesajes.Add(pesaje);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Pesaje_PesosConDecimalesDentroDeTolerancia_SeGuardaCorrectamente()
    {
        var (ganadero, cliente, usuario) = await CrearCatalogosBaseAsync("P8", 71014, "pesador_pesos_ok");

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(71014)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConPesos(100.1m, 99.9m)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var releido = await ctx.Pesajes.AsNoTracking().SingleAsync(p => p.Id == pesaje.Id);
        Assert.Equal(100.1m, releido.PesoIzquierdo);
        Assert.Equal(99.9m, releido.PesoDerecho);
        Assert.Equal(200.0m, releido.PesoTotal);
        Assert.Equal(OrigenPeso.Balanza, releido.OrigenPeso);
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

    [Fact]
    public async Task Usuario_IntentosFallidosNegativos_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var usuario = new UsuarioBuilder()
            .ConNombreUsuario("usr_invalido_intentos")
            .ConBloqueo(-1, null)
            .Build();

        ctx.Usuarios.Add(usuario);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Usuario_BloqueadoHasta_IdaYVueltaConservaUtc()
    {
        var bloqueadoHasta = new DateTime(2026, 3, 10, 15, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var usuario = new UsuarioBuilder()
            .ConNombreUsuario("usr_bloqueado_test")
            .ConBloqueo(5, bloqueadoHasta)
            .Build();

        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var releido = await ctx.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);

        Assert.Equal(5, releido.IntentosFallidos);
        Assert.NotNull(releido.BloqueadoHasta);
        Assert.Equal(DateTimeKind.Utc, releido.BloqueadoHasta!.Value.Kind);
        Assert.Equal(bloqueadoHasta, releido.BloqueadoHasta.Value);
    }

    [Fact]
    public async Task Ganadero_MarcaVacia_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var ganadero = new GanaderoBuilder().ConMarca("").ConNombre("Ganadero Sin Marca").Build();

        ctx.Ganaderos.Add(ganadero);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Ganadero_MarcaDeSeisCaracteres_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var ganadero = new GanaderoBuilder().ConMarca("ABCDEF").ConNombre("Ganadero Marca Larga").Build();

        ctx.Ganaderos.Add(ganadero);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Cliente_CodigoCero_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var cliente = new ClienteBuilder().ConCodigo(0).ConNombre("Cliente Codigo Cero").Build();

        ctx.Clientes.Add(cliente);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Cliente_CodigoMayorAlMaximo_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var cliente = new ClienteBuilder().ConCodigo(1_000_000).ConNombre("Cliente Codigo Millon").Build();

        ctx.Clientes.Add(cliente);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task ConfiguracionBalanza_LecturasParaEstableMayorA50_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.LecturasParaEstable = 51);

    [Fact]
    public async Task ConfiguracionBalanza_TimeoutMsMenorA500_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.TimeoutMs = 499);

    [Fact]
    public async Task ConfiguracionBalanza_BaudRateMenorA110_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.BaudRate = 100);

    [Fact]
    public async Task ConfiguracionBalanza_DataBitsMayorA8_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.DataBits = 9);

    [Fact]
    public async Task ConfiguracionBalanza_ParidadInvalida_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.Paridad = (ParidadSerial)7);

    [Fact]
    public async Task ConfiguracionBalanza_BitsParadaInvalido_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.BitsParada = (BitsParada)4);

    [Fact]
    public async Task ConfiguracionBalanza_PuertoTcpCero_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.PuertoTcp = 0);

    [Fact]
    public async Task ConfiguracionBalanza_PuertoTcpMayorA65535_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.PuertoTcp = 65536);

    [Fact]
    public async Task ConfiguracionBalanza_ToleranciaKgNegativa_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.ToleranciaKg = -0.1m);

    [Fact]
    public async Task ConfiguracionBalanza_ToleranciaKgMayorA10_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.ToleranciaKg = 10.1m);

    [Fact]
    public async Task ConfiguracionBalanza_PuertoComVacio_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.PuertoCom = "");

    [Fact]
    public async Task ConfiguracionBalanza_HostTcpVacio_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.HostTcp = "");

    [Fact]
    public async Task ConfiguracionBalanza_PatronRegexVacio_LanzaDbUpdateException() =>
        await ProbarCheckConstraintConfiguracionBalanzaAsync(c => c.PatronRegex = "");

    private async Task ProbarCheckConstraintConfiguracionBalanzaAsync(Action<ConfiguracionBalanza> mutar)
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();
        var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
        mutar(config);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
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
