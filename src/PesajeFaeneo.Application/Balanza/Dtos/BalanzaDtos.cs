using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Balanza.Dtos;

public sealed record ConfiguracionBalanzaDto(
    TipoConexion TipoConexion,
    string PuertoCom,
    int BaudRate,
    int DataBits,
    ParidadSerial Paridad,
    BitsParada BitsParada,
    string HostTcp,
    int PuertoTcp,
    string PatronRegex,
    int LecturasParaEstable,
    decimal ToleranciaKg,
    int TimeoutMs,
    DelimitadorTrama DelimitadorTrama = DelimitadorTrama.FinDeLinea,
    UnidadPesoTrama UnidadPeso = UnidadPesoTrama.Kilogramos);

public sealed record ConfiguracionBalanzaLeida(ConfiguracionBalanzaDto Configuracion, DateTime ModificadoEn);

public sealed record ResultadoTrama(bool Valida, decimal? PesoKg, bool? EstableReportado, string? Error)
{
    public static ResultadoTrama Invalida(string error) => new(false, null, null, error);
}

/// <summary>
/// Instantánea inmutable del estado de la balanza. Se reemplaza entera en cada publicación.
/// </summary>
public sealed record InstantaneaBalanza(
    long Secuencia,
    EstadoBalanza Estado,
    decimal? PesoKg,
    bool Estable,
    DateTimeOffset? InstanteLectura,
    string? Mensaje,
    string Descripcion,
    bool EsSimulador)
{
    public static InstantaneaBalanza Inicial { get; } =
        new(0, EstadoBalanza.Detenida, null, false, null, null, string.Empty, false);

    /// <summary>
    /// TimeoutMs de la configuración con la que se produjo la lectura (para EsCapturable sin leer la configuración).
    /// </summary>
    public int TimeoutMs { get; init; } = ReglasBalanza.TimeoutPorDefectoMs;

    /// <summary>
    /// Lo usará F4-05 para capturar: conectada, estable, con peso y con lectura reciente dentro de TimeoutMs.
    /// </summary>
    public bool EsCapturable(DateTimeOffset ahora) => EsCapturable(ahora, TimeSpan.FromMilliseconds(TimeoutMs));

    /// <summary>
    /// Lo usará F4-05 para capturar: conectada, estable, con peso y con lectura reciente.
    /// </summary>
    public bool EsCapturable(DateTimeOffset ahora, TimeSpan antiguedadMaxima) =>
        Estado == EstadoBalanza.Conectada
        && Estable
        && PesoKg.HasValue
        && InstanteLectura.HasValue
        && ahora - InstanteLectura.Value <= antiguedadMaxima;
}

public sealed record AvancePruebaBalanza(string TramaVisible, ResultadoTrama Resultado, bool Estable);

public sealed record ResultadoPruebaBalanza(
    bool Conecto,
    string? Error,
    int TramasRecibidas,
    int TramasValidas,
    decimal? UltimoPesoKg,
    bool AlcanzoEstabilidad,
    bool Cancelada);
