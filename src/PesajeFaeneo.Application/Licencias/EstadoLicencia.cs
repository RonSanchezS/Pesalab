namespace PesajeFaeneo.Application.Licencias;

public enum TipoEstadoLicencia
{
    Activada,
    Prueba,
    NoIniciada,
    Vencida
}

public enum MotivoVencimiento
{
    PruebaTerminada,
    Reloj,
    Alterada
}

/// <summary>
/// F13 (D3): estado de la licencia del equipo. <see cref="TipoEstadoLicencia.Vencida"/> equivale al modo consulta (D4): se puede
/// consultar, reimprimir, editar, anular y respaldar, pero no pesar ni importar datos legados.
/// </summary>
public sealed record EstadoLicencia(TipoEstadoLicencia Tipo, int DiasRestantes = 0, MotivoVencimiento? Motivo = null)
{
    public static EstadoLicencia Activada { get; } = new(TipoEstadoLicencia.Activada);

    public static EstadoLicencia NoIniciada { get; } = new(TipoEstadoLicencia.NoIniciada);

    public static EstadoLicencia EnPrueba(int diasRestantes) => new(TipoEstadoLicencia.Prueba, diasRestantes);

    public static EstadoLicencia VencidaPor(MotivoVencimiento motivo) => new(TipoEstadoLicencia.Vencida, 0, motivo);

    /// <summary>Solo una licencia activada o una prueba vigente permiten pesar e importar.</summary>
    public bool PermiteOperar => Tipo is TipoEstadoLicencia.Activada or TipoEstadoLicencia.Prueba;

    public string Texto => Tipo switch
    {
        TipoEstadoLicencia.Activada => "Activada",
        TipoEstadoLicencia.Prueba => DiasRestantes == 1 ? "Prueba (1 día restante)" : $"Prueba ({DiasRestantes} días restantes)",
        TipoEstadoLicencia.NoIniciada => "Sin activar",
        _ => Motivo switch
        {
            MotivoVencimiento.Reloj => "Modo consulta (cambio en el reloj del sistema)",
            MotivoVencimiento.Alterada => "Modo consulta (datos de la prueba alterados)",
            _ => "Modo consulta (prueba terminada)"
        }
    };
}
