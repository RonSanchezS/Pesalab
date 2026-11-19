using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Backup;
using PesajeFaeneo.Tests.Utilidades.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioImportacionTests
{
    private static async Task<(BaseDatosEnMemoria Db, FakeTimeProvider Tiempo, SesionActual Sesion, LectorOrigenDatosFalso Lector, MotorBackupFalso Motor, ServicioImportacion Servicio)> CrearEntornoAsync(Rol rol = Rol.Administrador)
    {
        var db = await BaseDatosEnMemoria.CrearAsync();
        var seeder = new DataSeeder(db.ContextFactory, new Pbkdf2PasswordHasher(), TimeProvider.System);
        await seeder.SembrarAsync();

        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));

        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db.ContextFactory, sesion, rol);

        var lector = new LectorOrigenDatosFalso();
        var motor = new MotorBackupFalso();
        var repo = new RepositorioImportacion(db.ContextFactory, NullLogger<RepositorioImportacion>.Instance);

        var servicio = new ServicioImportacion(
            lector,
            repo,
            motor,
            sesion,
            new PesajeFaeneo.Tests.Utilidades.Licencias.ServicioLicenciaFalso(),
            tiempo,
            NullLogger<ServicioImportacion>.Instance);

        return (db, tiempo, sesion, lector, motor, servicio);
    }

    [Fact]
    public async Task Test01_Supervisor_LanzaNoAutorizadoExceptionEnLosCuatroMetodos_YNoLlamaLector()
    {
        var (db, _, _, lector, _, servicio) = await CrearEntornoAsync(Rol.Supervisor);
        await using (db)
        {
            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, new Dictionary<string, int?>());

            await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarTablasAccessAsync("archivo.mdb"));
            await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.LeerOrigenAsync(lectura, TipoImportacion.Ganaderos));
            await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.SimularAsync(solicitud));
            await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ImportarAsync(solicitud, "huella"));

            Assert.Equal(0, lector.ConteoLecturas);
            Assert.Empty(lector.Solicitudes);
        }
    }

    [Fact]
    public async Task Test02_LeerOrigenAsync_Con30Filas_DevuelveMuestra20YTotalFilas30YMapeoSugerido()
    {
        var (db, _, _, lector, _, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            var filas = new List<FilaOrigen>(30);
            for (int i = 1; i <= 30; i++)
            {
                filas.Add(new FilaOrigen(i + 1, new[] { $"M{i}", $"GANADERO {i}", "true" }));
            }

            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                filas,
                "HUELLA_30_FILAS",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var vista = await servicio.LeerOrigenAsync(lectura, TipoImportacion.Ganaderos);

            Assert.Equal(20, vista.Muestra.Count);
            Assert.Equal(30, vista.TotalFilas);
            Assert.Equal("UTF-8", vista.Codificacion);
            Assert.Equal(3, vista.Columnas.Count);

            Assert.Equal(0, vista.MapeoSugerido[CamposImportacion.Codigo]);
            Assert.Equal(1, vista.MapeoSugerido[CamposImportacion.Nombre]);
            Assert.Equal(2, vista.MapeoSugerido[CamposImportacion.Activo]);
        }
    }

    [Fact]
    public async Task Test03_SimularAsync_Ganaderos_DevuelveInsertadasFilasAImportar_SinCambiosEnBdNiMotorNiAuditoria()
    {
        var (db, _, _, lector, motor, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "G1", "GANADERO 1", "true" }),
                    new(3, new[] { "G2", "GANADERO 2", "true" })
                },
                "HUELLA_SIM",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            var resultado = await servicio.SimularAsync(solicitud);

            Assert.True(resultado.EsSimulacro);
            Assert.Equal(2, resultado.FilasLeidas);
            Assert.Equal(2, resultado.FilasAImportar);
            Assert.Equal(2, resultado.Insertadas);
            Assert.Null(resultado.BackupPrevio);
            Assert.False(string.IsNullOrWhiteSpace(resultado.Huella));

            // Sin llamadas al motor
            Assert.Empty(motor.Solicitudes);

            // Sin cambios en BD
            await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
            {
                Assert.Equal(0, await ctx.Ganaderos.CountAsync());
                Assert.Equal(0, await ctx.Auditorias.CountAsync());
            }
        }
    }

    [Fact]
    public async Task Test04_ImportarAsync_ConHuellaSimulacro_CreaBackupPreImportacion_InsertaEnBdYAudita()
    {
        var (db, _, _, lector, motor, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "G1", "GANADERO 1", "true" }),
                    new(3, new[] { "G2", "GANADERO 2", "true" })
                },
                "HUELLA_TEST_04",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            var sim = await servicio.SimularAsync(solicitud);
            var imp = await servicio.ImportarAsync(solicitud, sim.Huella);

            Assert.False(imp.EsSimulacro);
            Assert.Equal(2, imp.Insertadas);
            Assert.Equal("fake.zip", imp.BackupPrevio);

            // El motor recibió PreImportacion
            Assert.Single(motor.Solicitudes);
            Assert.Equal(TipoBackup.PreImportacion, motor.Solicitudes[0].Tipo);

            // Filas en BD y auditoría
            await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
            {
                var ganaderos = await ctx.Ganaderos.OrderBy(g => g.Codigo).ToListAsync();
                Assert.Equal(2, ganaderos.Count);
                Assert.Equal("G1", ganaderos[0].Codigo);
                Assert.Equal("G2", ganaderos[1].Codigo);

                var auditorias = await ctx.Auditorias.ToListAsync();
                Assert.Single(auditorias);
                var aud = auditorias[0];
                Assert.Equal(AccionAuditoria.Importacion, aud.Accion);
                Assert.Contains("\"AImportar\":2", aud.DatosDespuesJson);
            }
        }
    }

    [Fact]
    public async Task Test05_ImportarAsync_ConOtraHuellaOTrasCambiarMapeo_LanzaReglaNegocioExceptionIMP_CAMBIO()
    {
        var (db, _, _, lector, motor, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "G1", "GANADERO 1", "true" })
                },
                "HUELLA_ORIGINAL",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            var sim = await servicio.SimularAsync(solicitud);

            // Con otra huella
            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                servicio.ImportarAsync(solicitud, "OTRA_HUELLA_DISTINTA"));
            Assert.Equal("IMP_CAMBIO", ex.Codigo);

            // Con mapeo cambiado
            var mapeoCambiado = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 1,
                [CamposImportacion.Nombre] = 0,
                [CamposImportacion.Activo] = 2
            };
            var solicitudModificada = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeoCambiado);
            var ex2 = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                servicio.ImportarAsync(solicitudModificada, sim.Huella));
            Assert.Equal("IMP_CAMBIO", ex2.Codigo);

            Assert.Empty(motor.Solicitudes);
            await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
            {
                Assert.Equal(0, await ctx.Ganaderos.CountAsync());
            }
        }
    }

    [Fact]
    public async Task Test06_ImportarAsync_MotorFalla_LanzaReglaNegocioExceptionIMP_SIN_RESGUARDO_SinCambios()
    {
        var (db, _, _, lector, motor, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "G1", "GANADERO 1", "true" })
                },
                "HUELLA_MOTOR_FALLA",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            var sim = await servicio.SimularAsync(solicitud);

            motor.ErrorCrear = new IOException("Disco lleno o error de E/S");

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                servicio.ImportarAsync(solicitud, sim.Huella));
            Assert.Equal("IMP_SIN_RESGUARDO", ex.Codigo);

            await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
            {
                Assert.Equal(0, await ctx.Ganaderos.CountAsync());
            }
        }
    }

    [Fact]
    public async Task Test07_ImportarAsync_TodasLasFilasConError_LanzaReglaNegocioExceptionIMP_NADA()
    {
        var (db, _, _, lector, _, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            // Fila con marca inválida
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "1234567890", "", "true" }) // Marca inválida y nombre vacío
                },
                "HUELLA_CON_ERROR",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            var sim = await servicio.SimularAsync(solicitud);
            Assert.Equal(0, sim.FilasAImportar);
            Assert.Equal(1, sim.FilasConError);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                servicio.ImportarAsync(solicitud, sim.Huella));
            Assert.Equal("IMP_NADA", ex.Codigo);
        }
    }

    [Fact]
    public async Task Test08_ImportarAsync_DosVecesMismoArchivo_SegundaVezLanzaIMP_NADA()
    {
        var (db, _, _, lector, _, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "G1", "GANADERO 1", "true" })
                },
                "HUELLA_IDEMPOTENTE",
                "UTF-8",
                Array.Empty<string>());

            var lectura = new SolicitudLectura("archivo.csv", FormatoOrigen.Csv, null, null);
            var mapeo = new Dictionary<string, int?>
            {
                [CamposImportacion.Codigo] = 0,
                [CamposImportacion.Nombre] = 1,
                [CamposImportacion.Activo] = 2
            };
            var solicitud = new SolicitudImportacion(lectura, TipoImportacion.Ganaderos, mapeo);

            // Primera importación
            var sim1 = await servicio.SimularAsync(solicitud);
            var imp1 = await servicio.ImportarAsync(solicitud, sim1.Huella);
            Assert.Equal(1, imp1.Insertadas);

            // Segunda importación (la marca ya existe en BD -> omitida -> FilasAImportar = 0)
            var sim2 = await servicio.SimularAsync(solicitud);
            Assert.Equal(0, sim2.FilasAImportar);
            Assert.Equal(1, sim2.FilasOmitidas);

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                servicio.ImportarAsync(solicitud, sim2.Huella));
            Assert.Equal("IMP_NADA", ex.Codigo);
        }
    }

    [Fact]
    public async Task Test09_FlujoEncadenado_GanaderosClientesPesajes_BoletasConRelacionesCorrectasYSiguienteBoletaActualizado()
    {
        var (db, _, _, lector, _, servicio) = await CrearEntornoAsync(Rol.Administrador);
        await using (db)
        {
            // 1. Importar Ganadero "G1"
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "MARCA", "NOMBRE", "ACTIVO" },
                new List<FilaOrigen> { new(2, new[] { "G1", "GANADERO 1", "true" }) },
                "HUELLA_GANADEROS",
                "UTF-8",
                Array.Empty<string>());

            var solG = new SolicitudImportacion(
                new SolicitudLectura("ganaderos.csv", FormatoOrigen.Csv, null, null),
                TipoImportacion.Ganaderos,
                new Dictionary<string, int?> { [CamposImportacion.Codigo] = 0, [CamposImportacion.Nombre] = 1, [CamposImportacion.Activo] = 2 });

            var simG = await servicio.SimularAsync(solG);
            await servicio.ImportarAsync(solG, simG.Huella);

            // 2. Importar Cliente con código 10
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "CODIGO", "NOMBRE", "DIRECCION", "ACTIVO" },
                new List<FilaOrigen> { new(2, new[] { "10", "CLIENTE 10", "CALLE PRINCIPAL", "true" }) },
                "HUELLA_CLIENTES",
                "UTF-8",
                Array.Empty<string>());

            var solC = new SolicitudImportacion(
                new SolicitudLectura("clientes.csv", FormatoOrigen.Csv, null, null),
                TipoImportacion.Clientes,
                new Dictionary<string, int?> { [CamposImportacion.Codigo] = 0, [CamposImportacion.Nombre] = 1, [CamposImportacion.Direccion] = 2, [CamposImportacion.Activo] = 3 });

            var simC = await servicio.SimularAsync(solC);
            await servicio.ImportarAsync(solC, simC.Huella);

            // 3. Importar Pesaje con boleta 500 para Ganadero G1 y Cliente 10
            lector.TablaAEntregar = new TablaOrigen(
                new[] { "NUMERO", "FECHA", "HORA", "MARCA", "CLIENTE", "PESO_IZQ", "PESO_DER", "PESO_TOTAL" },
                new List<FilaOrigen>
                {
                    new(2, new[] { "500", "01/10/2026", "10:30", "G1", "10", "125.4", "118.6", "244.0" })
                },
                "HUELLA_PESAJES",
                "UTF-8",
                Array.Empty<string>());

            var solP = new SolicitudImportacion(
                new SolicitudLectura("pesajes.csv", FormatoOrigen.Csv, null, null),
                TipoImportacion.Pesajes,
                new Dictionary<string, int?>
                {
                    [CamposImportacion.NumeroBoleta] = 0,
                    [CamposImportacion.Fecha] = 1,
                    [CamposImportacion.Hora] = 2,
                    [CamposImportacion.CodigoGanadero] = 3,
                    [CamposImportacion.CodigoCliente] = 4,
                    [CamposImportacion.PesoIzquierdo] = 5,
                    [CamposImportacion.PesoDerecho] = 6,
                    [CamposImportacion.PesoTotal] = 7
                });

            var simP = await servicio.SimularAsync(solP);
            var impP = await servicio.ImportarAsync(solP, simP.Huella);

            Assert.Equal(1, impP.Insertadas);

            await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
            {
                var pesaje = await ctx.Pesajes.Include(p => p.Ganadero).Include(p => p.Cliente).SingleAsync(p => p.NumeroBoleta == 500);
                Assert.Equal("G1", pesaje.Ganadero.Codigo);
                Assert.Equal(10, pesaje.Cliente.Codigo);
                Assert.Equal("CALLE PRINCIPAL", pesaje.Direccion);
                Assert.Equal(125.4m, pesaje.PesoIzquierdo);
                Assert.Equal(118.6m, pesaje.PesoDerecho);
                Assert.Equal(244.0m, pesaje.PesoTotal);
                Assert.Equal(OrigenPeso.Importado, pesaje.OrigenPeso);

                var param = await ctx.ParametrosSistema.FirstAsync();
                Assert.Equal(501, param.SiguienteNumeroBoleta);
            }
        }
    }
}
