namespace PesajeFaeneo.Application.Balanza;

public enum EstadoBalanza
{
    Detenida = 0,             // servicio no iniciado o detenido
    Conectando = 1,           // abriendo el puerto / conectando (incluye esperas de reintento)
    Conectada = 2,            // conectada; PesoKg != null si ya llegó al menos una lectura válida
    SinDatos = 3,             // conectada, pero sin tramas durante TimeoutMs
    TramasNoReconocidas = 4,  // llegan tramas, pero ninguna válida durante TimeoutMs
    Error = 5,                // falló la conexión o se perdió; se reintentará (Mensaje explica)
    EnDiagnostico = 6         // servicio pausado por ProbarAsync
}
