using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Logs;

public interface ILectorArchivosLog
{
    string Carpeta { get; }
    IReadOnlyList<ArchivoLog> Listar();
    (string Texto, bool Truncado, long TamanoBytes) LeerFinal(string nombreArchivo, long maximoBytes);
}
