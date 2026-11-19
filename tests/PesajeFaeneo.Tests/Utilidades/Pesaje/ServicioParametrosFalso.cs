using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class ServicioParametrosFalso : IServicioParametros
{
    public ParametrosDto Parametros { get; set; } = new(
        NombreEmpresa: "Empresa Test",
        Direccion: "Av. Principal 123",
        Telefono: "70000000",
        Ciudad: "Santa Cruz",
        RegistroSanitario: "RS-001",
        TelefonosReclamos: "800101010",
        ObservacionPorDefecto: "FAENEO NORMAL",
        LogoArchivo: null,
        RutaLogoCompleta: null,
        SiguienteNumeroBoleta: 101,
        UltimoNumeroBoletaEmitido: 100,
        ModificadoEn: DateTime.UtcNow);

    public int VecesObtenerLlamado { get; private set; }

    public Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        VecesObtenerLlamado++;
        return Task.FromResult(Parametros);
    }

    public Exception? ExcepcionIdentidad { get; set; }
    public TaskCompletionSource<IdentidadEmpresa>? IdentidadPendiente { get; set; }

    public Task<IdentidadEmpresa> ObtenerIdentidadEmpresaAsync(CancellationToken cancellationToken = default)
    {
        if (ExcepcionIdentidad is not null)
        {
            throw ExcepcionIdentidad;
        }

        return IdentidadPendiente?.Task ?? Task.FromResult(new IdentidadEmpresa(Parametros.NombreEmpresa, Parametros.RutaLogoCompleta));
    }

    public Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    // F11: tipo de pesaje guardado (null = sin definir) y boletas vigentes por tipo.
    public ModoDesposte? Modo { get; set; }
    public int BoletasResCompleta { get; set; }
    public int BoletasMediaRes { get; set; }
    public List<ModoDesposte> ModosGuardados { get; } = new();

    public Task<ModoDesposte> ObtenerModoDesposteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ReglasDesposte.Efectivo(Modo));

    public Task<ConfiguracionDesposte> ObtenerConfiguracionDesposteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConfiguracionDesposte(Modo, BoletasResCompleta, BoletasMediaRes));

    public Task CambiarModoDesposteAsync(ModoDesposte modo, CancellationToken cancellationToken = default)
    {
        ModosGuardados.Add(modo);
        Modo = modo;
        return Task.CompletedTask;
    }
}
