using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

public sealed class LectorOrigenDatos : ILectorOrigenDatos
{
    public const string MensajeSinMotor =
        "Para leer archivos de Access (.mdb/.accdb) instale «Microsoft Access Database Engine 2016 Redistributable» de 64 bits, o abra el archivo en Access y exporte la tabla a CSV.";

    private readonly ILogger<LectorOrigenDatos> _logger;

    internal long? MaximoBytesParaTest { get; set; }
    internal int? MaximoFilasParaTest { get; set; }

    public LectorOrigenDatos(ILogger<LectorOrigenDatos> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<string>> ListarTablasAsync(string rutaAccess, CancellationToken cancellationToken = default)
    {
        if (FormatosOrigen.DesdeExtension(rutaAccess) != FormatoOrigen.Access)
        {
            throw new ValidacionException(CamposOrigen.Tabla, "Solo los archivos de Access tienen tablas.");
        }

        return Task.Run<IReadOnlyList<string>>(() =>
        {
            if (!File.Exists(rutaAccess))
            {
                throw new ValidacionException(CamposOrigen.Archivo, "El archivo no existe.");
            }

            if (OperatingSystem.IsWindows())
            {
                return new LectorAccessOleDb().ListarTablas(rutaAccess);
            }

            throw new ReglaNegocioException("IMP_ACCESS_SIN_MOTOR", MensajeSinMotor);
        }, cancellationToken);
    }

    public async Task<TablaOrigen> LeerAsync(SolicitudLectura solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(solicitud.Ruta))
                {
                    throw new ValidacionException(CamposOrigen.Archivo, "El archivo no existe.");
                }

                if (solicitud.PaginaCodigos.HasValue && !ReglasImportacion.PaginasPermitidas.Contains(solicitud.PaginaCodigos.Value))
                {
                    throw new ValidacionException(CamposOrigen.Archivo, "Codificación no permitida.");
                }

                long maxBytes = MaximoBytesParaTest ?? (solicitud.Formato == FormatoOrigen.Access
                    ? ReglasImportacion.MaximoBytesAccess
                    : ReglasImportacion.MaximoBytesCsvDbf);

                var fi = new FileInfo(solicitud.Ruta);
                if (fi.Length > maxBytes)
                {
                    long mb = maxBytes / (1024 * 1024);
                    throw new ValidacionException(
                        CamposOrigen.Archivo,
                        mb > 0 ? $"El archivo pesa más de {mb} MB." : $"El archivo pesa más de {maxBytes} bytes.");
                }

                string huella;
                using (var fs = new FileStream(solicitud.Ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    huella = Convert.ToHexString(SHA256.HashData(fs));
                }

                TablaOrigen tabla;
                int maxFilas = MaximoFilasParaTest ?? ReglasImportacion.MaximoFilas;

                switch (solicitud.Formato)
                {
                    case FormatoOrigen.Csv:
                        {
                            byte[] bytes;
                            using (var fs = new FileStream(solicitud.Ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var ms = new MemoryStream())
                            {
                                fs.CopyTo(ms);
                                bytes = ms.ToArray();
                            }

                            tabla = new LectorCsv().Leer(bytes, solicitud.PaginaCodigos, huella, maxFilas);
                            break;
                        }

                    case FormatoOrigen.Dbf:
                        {
                            using var fs = new FileStream(solicitud.Ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var lector = new LectorDbf(fs, solicitud.PaginaCodigos, ReglasImportacion.PaginaPorDefecto);

                            var advertencias = new List<string>();
                            var columnas = NormalizarColumnas(lector.Columnas.Select(c => c.Nombre).ToList(), advertencias);

                            foreach (var col in lector.Columnas)
                            {
                                if (!col.Soportada)
                                {
                                    advertencias.Add($"La columna «{col.Nombre}» (tipo memo/no soportado) no se puede leer; quedará vacía.");
                                }
                            }

                            var filas = new List<FilaOrigen>();
                            while (true)
                            {
                                var valores = lector.LeerSiguiente();
                                if (valores == null)
                                {
                                    break;
                                }

                                filas.Add(new FilaOrigen(lector.NumeroRegistroActual, valores));
                                if (filas.Count > maxFilas)
                                {
                                    throw new ValidacionException(
                                        CamposOrigen.Archivo,
                                        $"El archivo tiene más de {ReglasImportacion.MaximoFilas.ToString("N0", TextosBoleta.Cultura)} filas; divídalo en partes.");
                                }
                            }

                            if (lector.Eliminados > 0)
                            {
                                advertencias.Add($"Se ignoraron {lector.Eliminados} registro(s) marcados como borrados.");
                            }

                            var codificacion = DetectorCodificacion.NombreDe(lector.PaginaDeCodigos);
                            tabla = new TablaOrigen(columnas, filas, huella, codificacion, advertencias);
                            break;
                        }

                    case FormatoOrigen.Access:
                        if (string.IsNullOrWhiteSpace(solicitud.Tabla))
                        {
                            throw new ValidacionException(CamposOrigen.Tabla, "Debe especificar la tabla a leer.");
                        }

                        if (OperatingSystem.IsWindows())
                        {
                            tabla = new LectorAccessOleDb().Leer(solicitud.Ruta, solicitud.Tabla, huella, maxFilas);
                        }
                        else
                        {
                            throw new ReglaNegocioException("IMP_ACCESS_SIN_MOTOR", MensajeSinMotor);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(solicitud), $"Formato no soportado: {solicitud.Formato}");
                }

                _logger.LogInformation(
                    "Archivo de importación leído: {Archivo} ({Formato}, {Filas} filas, {Codificacion}).",
                    Path.GetFileName(solicitud.Ruta),
                    solicitud.Formato,
                    tabla.Filas.Count,
                    tabla.Codificacion);

                return tabla;
            }
            catch (DomainException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                throw new ValidacionException(CamposOrigen.Archivo, ex.Message);
            }
            catch (IOException)
            {
                throw new ValidacionException(
                    CamposOrigen.Archivo,
                    "No se pudo abrir el archivo. Ciérrelo si está abierto en otro programa e intente de nuevo.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new ValidacionException(CamposOrigen.Archivo, "No tiene permiso para leer el archivo.");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<string> NormalizarColumnas(IReadOnlyList<string?> cabecera, List<string> advertencias)
    {
        var resultado = new List<string>(cabecera.Count);
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conteos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < cabecera.Count; i++)
        {
            var original = (cabecera[i] ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(original))
            {
                original = $"COLUMNA{i + 1}";
            }

            if (!conteos.TryGetValue(original, out var count))
            {
                conteos[original] = 1;
                existentes.Add(original);
                resultado.Add(original);
            }
            else
            {
                count++;
                conteos[original] = count;
                var nuevoNombre = $"{original}_{count}";
                while (existentes.Contains(nuevoNombre))
                {
                    count++;
                    conteos[original] = count;
                    nuevoNombre = $"{original}_{count}";
                }
                existentes.Add(nuevoNombre);
                string ordinal = count switch
                {
                    2 => "segunda",
                    3 => "tercera",
                    4 => "cuarta",
                    5 => "quinta",
                    _ => $"{count}ª"
                };
                advertencias.Add($"La columna «{original}» está repetida; la {ordinal} se llama «{nuevoNombre}».");
                resultado.Add(nuevoNombre);
            }
        }

        return resultado;
    }
}
