namespace PesajeFaeneo.Application.Utilitarios.Logs.Dtos;

public enum NivelLog
{
    Desconocido = 0,
    Detalle = 1,
    Depuracion = 2,
    Informacion = 3,
    Advertencia = 4,
    Error = 5,
    Fatal = 6
}

public sealed record ArchivoLog(string Nombre, DateOnly? Fecha, long TamanoBytes);

public sealed record EntradaLog(int Linea, string Marca, NivelLog Nivel, string Mensaje, string Detalle);

public sealed record ContenidoLog(string Archivo, IReadOnlyList<EntradaLog> Entradas, bool Truncado, long TamanoBytes);
