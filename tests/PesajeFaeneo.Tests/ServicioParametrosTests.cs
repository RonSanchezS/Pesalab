using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioParametrosTests
{
    private class AlmacenLogoFalso : IAlmacenLogo
    {
        public List<string> ArchivosGuardados { get; } = new();
        public List<string> ArchivosEliminados { get; } = new();

        public Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default)
        {
            var nombre = "logo-falso-" + Guid.NewGuid() + ".png";
            ArchivosGuardados.Add(nombre);
            return Task.FromResult(nombre);
        }

        public string? ObtenerRutaCompleta(string? nombreArchivo)
        {
            return string.IsNullOrEmpty(nombreArchivo) ? null : @"C:\fakepath\" + nombreArchivo;
        }

        public void Eliminar(string? nombreArchivo)
        {
            if (!string.IsNullOrEmpty(nombreArchivo))
            {
                ArchivosEliminados.Add(nombreArchivo);
            }
        }
    }

    private class UowFactoryQueFallaEnGuardar : IUnidadDeTrabajoFactory
    {
        private readonly IUnidadDeTrabajoFactory _inner;
        public UowFactoryQueFallaEnGuardar(IUnidadDeTrabajoFactory inner) => _inner = inner;

        public async Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default)
        {
            var uow = await _inner.CrearAsync(cancellationToken);
            return new UowDecorador(uow);
        }

        private class UowDecorador : IUnidadDeTrabajo
        {
            private readonly IUnidadDeTrabajo _inner;
            public UowDecorador(IUnidadDeTrabajo inner) => _inner = inner;

            public IUsuarioRepositorio Usuarios => _inner.Usuarios;
            public IAuditoriaRepositorio Auditoria => _inner.Auditoria;
            public IGanaderoRepositorio Ganaderos => _inner.Ganaderos;
            public IClienteRepositorio Clientes => _inner.Clientes;
            public IFleteroRepositorio Fleteros => _inner.Fleteros;
            public IParametrosRepositorio Parametros => _inner.Parametros;
            public IConfiguracionBalanzaRepositorio ConfiguracionBalanza => _inner.ConfiguracionBalanza;
            public IPesajeRepositorio Pesajes => _inner.Pesajes;

            public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Fallo simulado en GuardarCambiosAsync");
            }

            public Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default) =>
                _inner.IniciarTransaccionAsync(cancellationToken);

            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }

    private (ServicioParametros Servicio, ISesionActual Sesion, AlmacenLogoFalso Almacen, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        IUnidadDeTrabajoFactory? uowFactoryOverride = null,
        DateTime? tiempoInicial = null,
        AlmacenLogoFalso? almacenOverride = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var sesion = new SesionActual();
        var almacen = almacenOverride ?? new AlmacenLogoFalso();
        var factory = uowFactoryOverride ?? db.UnidadDeTrabajoFactory;
        var servicio = new ServicioParametros(factory, sesion, almacen, tiempo, NullLogger<ServicioParametros>.Instance);
        return (servicio, sesion, almacen, tiempo);
    }

    [Fact]
    public async Task Metodos_SinSesion_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1)));
    }

    [Fact]
    public async Task Metodos_ConSesionOperador_ObtenerFuncionaYActualizarLanzaNoAutorizado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var dto = await servicio.ObtenerAsync();
        Assert.NotNull(dto);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1)));
    }

    [Fact]
    public async Task ActualizarAsync_Valido_PersisteTextosSinMayusculasYGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTime(2026, 5, 20, 14, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _, _) = CrearServicio(db, tiempoInicial: fechaEsperada);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var dto = new ActualizarParametros(
            NombreEmpresa: "  Frigorífico   Central  ",
            Direccion: "  Av. Principal 123  ",
            Telefono: "3-3333333",
            Ciudad: "Santa Cruz",
            RegistroSanitario: "RS-999",
            TelefonosReclamos: "800-1010",
            ObservacionPorDefecto: "Sin observación previa",
            SiguienteNumeroBoleta: 100);

        await servicio.ActualizarAsync(dto);

        var obtenido = await servicio.ObtenerAsync();
        Assert.Equal("Frigorífico Central", obtenido.NombreEmpresa);
        Assert.Equal("Av. Principal 123", obtenido.Direccion);
        Assert.Equal(100, obtenido.SiguienteNumeroBoleta);
        Assert.Equal(fechaEsperada, obtenido.ModificadoEn);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.FirstOrDefaultAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema && a.Accion == AccionAuditoria.CambioParametros);
        Assert.NotNull(aud);
        Assert.Equal(usr.Id, aud.UsuarioId);
        Assert.Equal("1", aud.EntidadId);
        Assert.Contains("Central", aud.DatosDespuesJson);
    }

    [Fact]
    public async Task ActualizarAsync_SinCambios_NoGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var actual = await servicio.ObtenerAsync();
        var dto = new ActualizarParametros(
            actual.NombreEmpresa,
            actual.Direccion,
            actual.Telefono,
            actual.Ciudad,
            actual.RegistroSanitario,
            actual.TelefonosReclamos,
            actual.ObservacionPorDefecto,
            actual.SiguienteNumeroBoleta);

        await servicio.ActualizarAsync(dto);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var conteo = await ctx.Auditorias.CountAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema);
        Assert.Equal(0, conteo);
    }

    [Fact]
    public async Task ActualizarAsync_NombreVacio_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarAsync(new ActualizarParametros("", null, null, null, null, null, null, 1)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposParametros.NombreEmpresa);
    }

    [Fact]
    public async Task ActualizarAsync_NumeroBoletaConPesajes_NoSePuedeCambiarAMano()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        // Insertar un pesaje con boleta 500
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(500)
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(usr.Id)
                .Build();
            ctx.Pesajes.Add(pesaje);
            await ctx.SaveChangesAsync();
        }

        // D1 (F10): con pesajes emitidos el número es automático; ni 500 ni 501 (ni ningún otro) se aceptan a mano.
        var ex500 = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 500)));
        Assert.Equal("PAR_NUMERACION_AUTOMATICA", ex500.Codigo);
        var ex501 = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 501)));
        Assert.Equal("PAR_NUMERACION_AUTOMATICA", ex501.Codigo);
        var exAtras = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 2)));
        Assert.Equal("PAR_NUMERACION_AUTOMATICA", exAtras.Codigo);

        var obtenido = await servicio.ObtenerAsync();
        Assert.Equal(1, obtenido.SiguienteNumeroBoleta);
        Assert.False(obtenido.NumeracionEditable);
    }

    [Fact]
    public async Task ActualizarAsync_NumeroBoletaConSoloPesajeAnulado_TambienSeRechaza()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(7)
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(usr.Id)
                .Anulado("ERROR DE DIGITACION", usr.Id)
                .Build());
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 50)));
        Assert.Equal("PAR_NUMERACION_AUTOMATICA", ex.Codigo);
    }

    [Fact]
    public async Task ActualizarAsync_SinPesajes_PermiteFijarNumeroInicialYAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var antes = await servicio.ObtenerAsync();
        Assert.True(antes.NumeracionEditable);

        await servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 5000));

        var obtenido = await servicio.ObtenerAsync();
        Assert.Equal(5000, obtenido.SiguienteNumeroBoleta);
        Assert.True(obtenido.NumeracionEditable);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema && a.Accion == AccionAuditoria.CambioParametros);
        Assert.Contains("numeración", aud.Motivo);
    }

    [Fact]
    public async Task ActualizarAsync_NumeroSinCambiosAunqueSeaMenorAUltimoPesaje_NoFalla()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        // Estado inicial de parámetros tiene SiguienteNumeroBoleta = 1 (seed)
        // Insertamos un pesaje con boleta 1000
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(1000)
                .ConGanaderoId(g.Id)
                .ConClienteId(c.Id)
                .ConUsuarioId(usr.Id)
                .Build();
            ctx.Pesajes.Add(pesaje);
            await ctx.SaveChangesAsync();
        }

        // Modificamos solo el nombre de la empresa manteniendo SiguienteNumeroBoleta = 1
        await servicio.ActualizarAsync(new ActualizarParametros("Nuevo Nombre Empresa", null, null, null, null, null, null, 1));
        var obtenido = await servicio.ObtenerAsync();
        Assert.Equal("Nuevo Nombre Empresa", obtenido.NombreEmpresa);
    }

    [Fact]
    public async Task ActualizarAsync_LogoReemplazarYQuitar_GestionaArchivosYPersistencia()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, almacen, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        // 1. Reemplazar logo
        await servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1,
            AccionLogo: AccionLogo.Reemplazar, RutaLogoOrigen: "C:\\orig.png"));

        Assert.Single(almacen.ArchivosGuardados);
        var primerLogo = almacen.ArchivosGuardados[0];
        var params1 = await servicio.ObtenerAsync();
        Assert.Equal(primerLogo, params1.LogoArchivo);

        // 2. Reemplazar otra vez (el anterior debe eliminarse)
        await servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1,
            AccionLogo: AccionLogo.Reemplazar, RutaLogoOrigen: "C:\\orig2.png"));

        Assert.Equal(2, almacen.ArchivosGuardados.Count);
        Assert.Contains(primerLogo, almacen.ArchivosEliminados);

        // 3. Quitar logo
        var segundoLogo = almacen.ArchivosGuardados[1];
        await servicio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1,
            AccionLogo: AccionLogo.Quitar));

        var params3 = await servicio.ObtenerAsync();
        Assert.Null(params3.LogoArchivo);
        Assert.Contains(segundoLogo, almacen.ArchivosEliminados);
    }

    [Fact]
    public async Task ActualizarAsync_SiFallaGuardado_EliminaArchivoNuevoYConservaAnterior()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        // Crear servicio con logo previo ya guardado
        var (servicioPrevio, sesion, almacen, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await servicioPrevio.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1,
            AccionLogo.Reemplazar, "C:\\logo_inicial.png"));

        var logoInicial = almacen.ArchivosGuardados[0];

        // Crear servicio con UoW que falla al guardar cambios
        var (servicioFalla, sesionFalla, _, _) = CrearServicio(db, new UowFactoryQueFallaEnGuardar(db.UnidadDeTrabajoFactory), almacenOverride: almacen);
        await SesionesDePrueba.IniciarAsync(db, sesionFalla, Rol.Administrador, nombreUsuario: "usuario_falla");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicioFalla.ActualizarAsync(new ActualizarParametros("Empresa", null, null, null, null, null, null, 1,
                AccionLogo.Reemplazar, "C:\\logo_nuevo.png")));

        var logoNuevo = almacen.ArchivosGuardados[1];
        Assert.Contains(logoNuevo, almacen.ArchivosEliminados);
        Assert.DoesNotContain(logoInicial, almacen.ArchivosEliminados);
    }
}
