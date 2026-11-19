using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using PesajeFaeneo.Tests.Utilidades.Backup;
using PesajeFaeneo.Tests.Utilidades.Importacion;
using PesajeFaeneo.Tests.Utilidades.Licencias;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F13 (D4): en modo consulta no se graban boletas ni se importa/simula; el bloqueo va antes de cualquier transacción.</summary>
public class ModoConsultaServiciosTests
{
    private static readonly EstadoLicencia Vencida = EstadoLicencia.VencidaPor(MotivoVencimiento.PruebaTerminada);

    private static RegistrarPesaje Dto(int ganaderoId, int clienteId) => new(
        GanaderoId: ganaderoId,
        ClienteId: clienteId,
        Direccion: "Calle Test 123",
        Secuencia: 1,
        ColorRaza: "Overo",
        Sexo: 'M',
        TipoDesposte: TipoDesposte.ResCompleta,
        PesoIzquierdo: 120.0m,
        PesoDerecho: 118.5m,
        Observacion: "Obs",
        CapturadoConSimulador: false);

    private static ServicioPesajes CrearPesajes(BaseDatosEnMemoria db, SesionActual sesion, ServicioLicenciaFalso licencia)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.FromHours(-4)));
        var balanza = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(1, EstadoBalanza.Conectada, 120.0m, true, DateTimeOffset.UtcNow, null, "Conectada", false)
        };
        return new ServicioPesajes(db.UnidadDeTrabajoFactory, sesion, balanza, licencia, tiempo, NullLogger<ServicioPesajes>.Instance);
    }

    private static async Task<(int GanaderoId, int ClienteId)> SembrarAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConCodigo("1").ConNombre("GANADERO TEST").Build();
        var cliente = new ClienteBuilder().ConCodigo(100).ConNombre("CLIENTE TEST").Build();
        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        await ctx.SaveChangesAsync();
        return (ganadero.Id, cliente.Id);
    }

    [Fact]
    public async Task RegistrarAsync_EnModoConsulta_LanzaLicModoConsulta_SinConsumirNumeroNiGrabar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var licencia = new ServicioLicenciaFalso { Estado = Vencida };
        var servicio = CrearPesajes(db, sesion, licencia);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.RegistrarAsync(Dto(g, c)));

        Assert.Equal("LIC_MODO_CONSULTA", ex.Codigo);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await ctx.Pesajes.CountAsync());
        Assert.Equal(0, await ctx.Auditorias.CountAsync(a => a.Entidad == "Pesaje"));
        Assert.Equal(1, (await ctx.ParametrosSistema.SingleAsync()).SiguienteNumeroBoleta);
    }

    [Fact]
    public async Task RegistrarAsync_EnModoConsulta_RechazaAntesDeValidarElDto()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var servicio = CrearPesajes(db, sesion, new ServicioLicenciaFalso { Estado = EstadoLicencia.NoIniciada });
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.RegistrarAsync(Dto(0, 0)));

        Assert.Equal("LIC_MODO_CONSULTA", ex.Codigo);
    }

    [Fact]
    public async Task RegistrarAsync_ConLicenciaOPruebaVigente_GrabaYConsultaLaLicenciaUnaVez()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.EnPrueba(3) };
        var servicio = CrearPesajes(db, sesion, licencia);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        var r = await servicio.RegistrarAsync(Dto(g, c));

        Assert.Equal(1, r.NumeroBoleta);
        Assert.Equal(1, licencia.VecesExigido);
    }

    [Fact]
    public async Task RegistrarAsync_ConElServicioRealDeLicencia_UnaPruebaQueVenceDuranteLaSesionSeDetectaAlGrabar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var almacen = new AlmacenLicenciaFalso();
        var equipo = new IdentificadorEquipoFalso();
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
        almacen.SembrarPrueba(equipo.Hwid, tiempo.GetUtcNow().ToUnixTimeSeconds(), tiempo.GetUtcNow().ToUnixTimeSeconds());
        var licencia = new ServicioLicencia(almacen, new ServidorLicenciasFalso(), equipo, tiempo, NullLogger<ServicioLicencia>.Instance);
        var balanza = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(1, EstadoBalanza.Conectada, 120.0m, true, DateTimeOffset.UtcNow, null, "Conectada", false)
        };
        var servicio = new ServicioPesajes(db.UnidadDeTrabajoFactory, sesion, balanza, licencia, tiempo, NullLogger<ServicioPesajes>.Instance);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        await servicio.RegistrarAsync(Dto(g, c));
        tiempo.Advance(TimeSpan.FromDays(16));
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.RegistrarAsync(Dto(g, c) with { Secuencia = 2 }));

        Assert.Equal("LIC_MODO_CONSULTA", ex.Codigo);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.Pesajes.CountAsync());
    }

    [Fact]
    public async Task EnModoConsulta_ListarYSugerirSecuencia_SiguenFuncionando()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var servicio = CrearPesajes(db, sesion, new ServicioLicenciaFalso { Estado = Vencida });
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, _) = await SembrarAsync(db);

        Assert.Empty(await servicio.ListarDelDiaAsync());
        Assert.Equal(1, (await servicio.SugerirSecuenciaAsync(g)).Siguiente);
    }

    // ------------------------------------------------------------ importación

    private static async Task<(BaseDatosEnMemoria Db, LectorOrigenDatosFalso Lector, MotorBackupFalso Motor, ServicioImportacion Servicio)> CrearImportacionAsync(
        ServicioLicenciaFalso licencia)
    {
        var db = await BaseDatosEnMemoria.CrearAsync();
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 14, 0, 0, TimeSpan.Zero));
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db.ContextFactory, sesion, Rol.Administrador);
        var lector = new LectorOrigenDatosFalso();
        var motor = new MotorBackupFalso();
        var repo = new RepositorioImportacion(db.ContextFactory, NullLogger<RepositorioImportacion>.Instance);
        var servicio = new ServicioImportacion(lector, repo, motor, sesion, licencia, tiempo, NullLogger<ServicioImportacion>.Instance);
        return (db, lector, motor, servicio);
    }

    private static SolicitudImportacion SolicitudGanaderos() => new(
        new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null),
        TipoImportacion.Ganaderos,
        new Dictionary<string, int?> { [CamposImportacion.Codigo] = 0, [CamposImportacion.Nombre] = 1 });

    [Fact]
    public async Task SimularEImportar_EnModoConsulta_LanzanLicModoConsulta_SinLeerElOrigenNiCrearBackup()
    {
        var (db, lector, motor, servicio) = await CrearImportacionAsync(new ServicioLicenciaFalso { Estado = Vencida });
        await using (db)
        {
            var solicitud = SolicitudGanaderos();

            var exSimular = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.SimularAsync(solicitud));
            var exImportar = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ImportarAsync(solicitud, "huella"));

            Assert.Equal("LIC_MODO_CONSULTA", exSimular.Codigo);
            Assert.Equal("LIC_MODO_CONSULTA", exImportar.Codigo);
            Assert.Equal(0, lector.ConteoLecturas);
            Assert.Empty(motor.Solicitudes);
            await using var ctx = await db.ContextFactory.CreateDbContextAsync();
            Assert.Equal(0, await ctx.Ganaderos.CountAsync());
        }
    }

    [Fact]
    public async Task Simular_ConLicenciaVigente_ConsultaLaLicenciaYSimula()
    {
        var licencia = new ServicioLicenciaFalso { Estado = EstadoLicencia.Activada };
        var (db, lector, _, servicio) = await CrearImportacionAsync(licencia);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE" },
                new List<FilaOrigen> { new(2, new[] { "G1", "GANADERO 1" }) },
                "HUELLA",
                "UTF-8",
                Array.Empty<string>());

            var r = await servicio.SimularAsync(SolicitudGanaderos());

            Assert.True(r.EsSimulacro);
            Assert.Equal(1, licencia.VecesExigido);
        }
    }

    [Fact]
    public async Task LeerOrigenYListarTablas_EnModoConsulta_NoSeBloquean()
    {
        var (db, lector, _, servicio) = await CrearImportacionAsync(new ServicioLicenciaFalso { Estado = Vencida });
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(new[] { "MARCA", "NOMBRE" }, new List<FilaOrigen>(), "H", "UTF-8", Array.Empty<string>());

            var vista = await servicio.LeerOrigenAsync(SolicitudGanaderos().Lectura, TipoImportacion.Ganaderos);

            Assert.Equal(0, vista.TotalFilas);
        }
    }
}
