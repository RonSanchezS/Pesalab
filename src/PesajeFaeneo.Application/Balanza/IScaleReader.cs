using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.Application.Balanza;

/// <summary>
/// Una conexión física (o simulada) con la balanza. Una instancia = un intento de conexión:
/// tras un error se desecha y se crea otra con la fábrica. No es seguro para uso concurrente.
/// </summary>
public interface IScaleReader : IAsyncDisposable
{
    /// <summary>
    /// Texto corto para la UI: "COM3 9600 8N1", "192.168.1.100:4001" o "Simulador".
    /// </summary>
    string Descripcion { get; }

    /// <summary>
    /// Abre el puerto o la conexión. Lanza ConexionBalanzaException con un mensaje apto para el
    /// usuario si no puede. Puede lanzar OperationCanceledException si se cancela el token.
    /// </summary>
    Task ConectarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Espera la siguiente trama completa (sin delimitadores). Lanza ConexionBalanzaException si la
    /// conexión se perdió y OperationCanceledException si se cancela el token (sin perder tramas:
    /// una trama no entregada sigue disponible para la próxima llamada).
    /// </summary>
    ValueTask<string> LeerTramaAsync(CancellationToken cancellationToken);
}

public interface IFabricaLectorBalanza
{
    IScaleReader Crear(ConfiguracionBalanzaDto configuracion);
}

/// <summary>
/// Estado compartido del simulador (singleton). Lo manipula FrmConfiguracionBalanza.
/// </summary>
public interface ISimuladorBalanza
{
    decimal PesoObjetivoKg { get; }

    /// <summary>
    /// Recorta a [0, PesoMaximoSimuladorKg] y redondea a 0,1 (AwayFromZero).
    /// </summary>
    void EstablecerPesoObjetivo(decimal pesoKg);

    /// <summary>
    /// Si es true, ConectarAsync del lector simulado falla y un lector ya conectado pierde la conexión.
    /// </summary>
    bool DesconexionSimulada { get; set; }
}

public interface IEnumeradorPuertosSerie
{
    /// <summary>
    /// Nombres de puerto COM presentes, sin duplicados, normalizados (Trim + mayúsculas) y ordenados
    /// por número (COM2 antes que COM10). Nunca lanza: ante error devuelve lista vacía.
    /// </summary>
    IReadOnlyList<string> Listar();
}
