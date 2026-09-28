using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;

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

    public Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
