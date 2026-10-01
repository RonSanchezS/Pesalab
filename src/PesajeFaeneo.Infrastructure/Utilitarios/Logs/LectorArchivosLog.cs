using System.Text;
using PesajeFaeneo.Application.Utilitarios.Logs;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Logs;

public sealed class LectorArchivosLog : ILectorArchivosLog
{
    private readonly AppPaths _appPaths;

    public LectorArchivosLog(AppPaths appPaths)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
    }

    public string Carpeta => _appPaths.LogsDir;

    public IReadOnlyList<ArchivoLog> Listar()
    {
        if (!Directory.Exists(Carpeta))
        {
            return Array.Empty<ArchivoLog>();
        }

        var archivos = Directory.GetFiles(Carpeta, "pesaje-*.log");
        var lista = new List<ArchivoLog>();

        foreach (var ruta in archivos)
        {
            var nombre = Path.GetFileName(ruta);
            if (!ParserLog.EsNombreValido(nombre))
            {
                continue;
            }

            var fi = new FileInfo(ruta);
            var fecha = ParserLog.FechaDeNombre(nombre);
            lista.Add(new ArchivoLog(nombre, fecha, fi.Length));
        }

        return lista.OrderByDescending(a => a.Nombre, StringComparer.Ordinal).ToList();
    }

    public (string Texto, bool Truncado, long TamanoBytes) LeerFinal(string nombreArchivo, long maximoBytes)
    {
        if (!ParserLog.EsNombreValido(nombreArchivo))
        {
            throw new ValidacionException("Archivo", "Nombre de archivo de log no válido.");
        }

        var ruta = Path.Combine(Carpeta, nombreArchivo);

        try
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long tamanoBytes = fs.Length;
            bool truncado = false;

            if (tamanoBytes > maximoBytes)
            {
                fs.Seek(tamanoBytes - maximoBytes, SeekOrigin.Begin);
                truncado = true;
            }

            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            var bytes = ms.ToArray();
            var texto = Encoding.UTF8.GetString(bytes);

            if (truncado)
            {
                int idx = texto.IndexOf('\n');
                texto = idx >= 0 ? (idx + 1 < texto.Length ? texto[(idx + 1)..] : string.Empty) : string.Empty;
            }

            return (texto, truncado, tamanoBytes);
        }
        catch (FileNotFoundException)
        {
            throw new ValidacionException("Archivo", "El archivo de log ya no existe.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ValidacionException("Archivo", "No se pudo leer el archivo de log.");
        }
    }
}
