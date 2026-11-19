using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Boletas;

public class ServicioBoletas : IServicioBoletas
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly IAlmacenLogo _almacenLogo;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioBoletas> _logger;

    public ServicioBoletas(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        IAlmacenLogo almacenLogo,
        TimeProvider timeProvider,
        ILogger<ServicioBoletas> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _almacenLogo = almacenLogo ?? throw new ArgumentNullException(nameof(almacenLogo));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatosBoleta> ObtenerPorIdAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirSesion();
        if (!_sesion.TienePermiso(Permiso.ImprimirBoleta) && !_sesion.TienePermiso(Permiso.ReimprimirBoleta))
        {
            throw new NoAutorizadoException();
        }

        if (pesajeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pesajeId));
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Pesajes.ObtenerParaBoletaPorIdAsync(pesajeId, cancellationToken);
        if (p is null)
        {
            throw new EntidadNoEncontradaException("Pesaje", pesajeId);
        }

        return await MapearAsync(uow, p, cancellationToken);
    }

    public async Task<DatosBoleta> ObtenerPorNumeroAsync(long numeroBoleta, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirSesion();
        if (!_sesion.TienePermiso(Permiso.ImprimirBoleta) && !_sesion.TienePermiso(Permiso.ReimprimirBoleta))
        {
            throw new NoAutorizadoException();
        }

        if (numeroBoleta <= 0)
        {
            throw new ValidacionException(CamposBoleta.Numero, "Ingrese un número de boleta válido.");
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var p = await uow.Pesajes.ObtenerParaBoletaPorNumeroAsync(numeroBoleta, cancellationToken);
        if (p is null)
        {
            throw new ValidacionException(CamposBoleta.Numero, $"No existe la boleta N° {numeroBoleta}.");
        }

        return await MapearAsync(uow, p, cancellationToken);
    }

    public async Task RegistrarImpresionAsync(RegistroImpresion registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var sesion = _sesion.ExigirSesion();
        _sesion.ExigirPermiso(registro.EsReimpresion ? Permiso.ReimprimirBoleta : Permiso.ImprimirBoleta);

        if (registro.PesajeId <= 0 || registro.Copias < ReglasImpresion.CopiasMinimas || registro.Copias > ReglasImpresion.CopiasMaximas)
        {
            throw new ArgumentOutOfRangeException(nameof(registro));
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var accion = registro.EsReimpresion ? AccionAuditoria.Reimprimir : AccionAuditoria.Imprimir;
        var motivo = $"{(registro.EsReimpresion ? "Reimpresión" : "Impresión")} de boleta {registro.NumeroBoleta}: {registro.Copias} copia(s) en '{registro.Impresora}'";

        var entrada = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            accion,
            EntidadesAuditoria.Pesaje,
            registro.PesajeId.ToString(CultureInfo.InvariantCulture),
            despues: FabricaAuditoria.CrearSnapshot(registro),
            motivo: motivo);

        uow.Auditoria.Registrar(entrada);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation(
            "Boleta {Numero} {Tipo} en {Impresora} ({Copias} copias) por {Usuario}",
            registro.NumeroBoleta,
            registro.EsReimpresion ? "reimpresa" : "impresa",
            registro.Impresora,
            registro.Copias,
            sesion.NombreUsuario);
    }

    public async Task<DatosBoleta> CrearMuestraAsync(CancellationToken cancellationToken = default)
    {
        var sesion = _sesion.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var par = await uow.Parametros.ObtenerAsync(cancellationToken);
        var rutaLogo = _almacenLogo.ObtenerRutaCompleta(par.LogoPath);
        var empresa = new EmpresaBoleta(
            par.NombreEmpresa,
            par.Direccion,
            par.Ciudad,
            par.Telefono,
            par.RegistroSanitario,
            par.TelefonosReclamos,
            rutaLogo);

        // La muestra lleva un fletero de ejemplo para poder revisar dónde se imprime (F10, O9).
        var fletero = new FleteroBoleta(1, "FLETERO DE MUESTRA", "70012345");

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var ahoraLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, _timeProvider.LocalTimeZone), DateTimeKind.Unspecified);

        var observacion = string.IsNullOrWhiteSpace(par.ObservacionPorDefecto) ? "SIN NOVEDAD" : par.ObservacionPorDefecto;

        // F11: la muestra usa el tipo que pesa la empresa (res completa si pesa ambos).
        var modo = ReglasDesposte.Efectivo(par.ModoDesposte);
        var tipoMuestra = ReglasDesposte.TipoUnico(modo) ?? TipoDesposte.ResCompleta;
        var esMedia = tipoMuestra == TipoDesposte.MediaRes;

        return new DatosBoleta(
            PesajeId: 0,
            NumeroBoleta: 0,
            FechaHoraLocal: ahoraLocal,
            Empresa: empresa,
            CodigoGanadero: "1",
            NombreGanadero: "GANADERO DE MUESTRA",
            Secuencia: 1,
            CodigoCliente: 110,
            NombreCliente: "CLIENTE DE MUESTRA",
            Direccion: "DIRECCIÓN DE MUESTRA",
            ColorRaza: "OVERO",
            Sexo: 'M',
            TipoDesposte: tipoMuestra,
            PesoIzquierdo: 120.0m,
            PesoDerecho: esMedia ? 0m : 118.5m,
            PesoTotal: esMedia ? 120.0m : 238.5m,
            Observacion: observacion,
            Origen: OrigenPeso.Balanza,
            Estado: EstadoPesaje.Vigente,
            MotivoAnulacion: null,
            NombreOperador: sesion.NombreCompleto,
            Fletero: fletero,
            ImpresionesPrevias: 0,
            GeneradoEnLocal: ahoraLocal,
            EsMuestra: true,
            MostrarDesposte: ReglasDesposte.MostrarDesposte(modo, tipoMuestra));
    }

    private async Task<DatosBoleta> MapearAsync(IUnidadDeTrabajo uow, Pesaje p, CancellationToken ct)
    {
        var previas = await uow.Auditoria.ContarImpresionesPesajeAsync(p.Id, ct);
        if (previas > 0 && !_sesion.TienePermiso(Permiso.ReimprimirBoleta))
        {
            throw new NoAutorizadoException("No tiene permiso para reimprimir boletas.");
        }

        if (previas == 0 && !_sesion.TienePermiso(Permiso.ImprimirBoleta))
        {
            throw new NoAutorizadoException("No tiene permiso para imprimir boletas.");
        }

        var par = await uow.Parametros.ObtenerAsync(ct);
        var rutaLogo = _almacenLogo.ObtenerRutaCompleta(par.LogoPath);
        var empresa = new EmpresaBoleta(
            par.NombreEmpresa,
            par.Direccion,
            par.Ciudad,
            par.Telefono,
            par.RegistroSanitario,
            par.TelefonosReclamos,
            rutaLogo);

        var fletero = p.Fletero is null ? null : new FleteroBoleta(p.Fletero.Codigo, p.Fletero.Nombre, p.Fletero.Celular);

        var fechaHoraUtc = DateTime.SpecifyKind(p.FechaHora, DateTimeKind.Utc);
        var fechaHoraLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(fechaHoraUtc, _timeProvider.LocalTimeZone), DateTimeKind.Unspecified);

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var generadoEnLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, _timeProvider.LocalTimeZone), DateTimeKind.Unspecified);

        return new DatosBoleta(
            PesajeId: p.Id,
            NumeroBoleta: p.NumeroBoleta,
            FechaHoraLocal: fechaHoraLocal,
            Empresa: empresa,
            CodigoGanadero: p.Ganadero.Codigo ?? string.Empty,
            NombreGanadero: p.Ganadero.Nombre ?? string.Empty,
            Secuencia: p.Secuencia,
            CodigoCliente: p.Cliente.Codigo,
            NombreCliente: p.Cliente.Nombre ?? string.Empty,
            Direccion: p.Direccion ?? string.Empty,
            ColorRaza: p.ColorRaza ?? string.Empty,
            Sexo: p.Sexo,
            TipoDesposte: p.TipoDesposte,
            PesoIzquierdo: p.PesoIzquierdo,
            PesoDerecho: p.PesoDerecho,
            PesoTotal: p.PesoTotal,
            Observacion: p.Observacion ?? string.Empty,
            Origen: p.OrigenPeso,
            Estado: p.Estado,
            MotivoAnulacion: p.MotivoAnulacion,
            NombreOperador: p.Usuario.NombreCompleto ?? string.Empty,
            Fletero: fletero,
            ImpresionesPrevias: previas,
            GeneradoEnLocal: generadoEnLocal,
            EsMuestra: false,
            MostrarDesposte: ReglasDesposte.MostrarDesposte(ReglasDesposte.Efectivo(par.ModoDesposte), p.TipoDesposte));
    }
}
