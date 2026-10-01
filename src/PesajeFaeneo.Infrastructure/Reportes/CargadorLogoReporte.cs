using Microsoft.Extensions.Logging;
using PesajeFaeneo.Infrastructure.Archivos;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Infrastructure;

namespace PesajeFaeneo.Infrastructure.Reportes;

/// <summary>
/// Logo ya decodificado: Datos (bytes originales, para el Excel) e Imagen (para QuestPDF). Si existe, es válido.
/// </summary>
internal sealed record LogoReporte(byte[] Datos, QuestPDF.Infrastructure.Image Imagen);

internal static class CargadorLogoReporte
{
    static CargadorLogoReporte()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// null si no hay logo configurado o si no se puede usar (falta, > 1 MB, ilegible o no es una imagen):
    /// el reporte sale sin logo y queda una advertencia en el log (D10). Nunca lanza por el logo.
    /// </summary>
    public static LogoReporte? Cargar(string? ruta, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return null;
        }

        string nombreArchivo = Path.GetFileName(ruta);

        if (!File.Exists(ruta))
        {
            logger.LogWarning("No se pudo usar el logo «{Archivo}» en el reporte (no existe). Se generará sin logo.", nombreArchivo);
            return null;
        }

        long tamano;
        try
        {
            tamano = new FileInfo(ruta).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El archivo pudo desaparecer entre File.Exists y aquí: nunca se lanza por el logo.
            logger.LogWarning(ex, "No se pudo usar el logo «{Archivo}» en el reporte (no se pudo leer). Se generará sin logo.", nombreArchivo);
            return null;
        }

        if (tamano > AlmacenLogoArchivos.TamanoMaximoBytes)
        {
            logger.LogWarning("No se pudo usar el logo «{Archivo}» en el reporte (supera 1 MB). Se generará sin logo.", nombreArchivo);
            return null;
        }

        byte[] datos;
        try
        {
            datos = File.ReadAllBytes(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "No se pudo usar el logo «{Archivo}» en el reporte (no se pudo leer). Se generará sin logo.", nombreArchivo);
            return null;
        }

        // QuestPDF (y ClosedXML) solo miran la cabecera: un archivo cortado más adelante pasaría y quedaría como una
        // imagen rota dentro del PDF/Excel (R62). Se comprueba primero que el archivo esté completo.
        if (!EstaCompleto(datos))
        {
            logger.LogWarning("No se pudo usar el logo «{Archivo}» en el reporte (no es una imagen válida). Se generará sin logo.", nombreArchivo);
            return null;
        }

        QuestPDF.Infrastructure.Image imagen;
        try
        {
            imagen = QuestPDF.Infrastructure.Image.FromBinaryData(datos);
        }
        catch (Exception ex) when (ex is DocumentComposeException or ArgumentException)
        {
            logger.LogWarning(ex, "No se pudo usar el logo «{Archivo}» en el reporte (no es una imagen válida). Se generará sin logo.", nombreArchivo);
            return null;
        }

        return new LogoReporte(datos, imagen);
    }

    /// <summary>
    /// Integridad estructural de los formatos que admite el almacén de logos (PNG, JPEG, BMP): detecta archivos
    /// cortados sin decodificar el bitmap. PNG: recorre todos los bloques verificando su CRC hasta IEND. JPEG: termina
    /// en el marcador EOI (se toleran ceros de relleno al final). BMP: el tamaño declarado en la cabecera cabe en el
    /// archivo. Un formato que no sea ninguno de los tres lo decide QuestPDF.
    /// </summary>
    internal static bool EstaCompleto(byte[] d)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47)
        {
            return PngCompleto(d);
        }

        if (d.Length >= 2 && d[0] == 0xFF && d[1] == 0xD8)
        {
            int fin = d.Length;
            while (fin > 2 && d[fin - 1] == 0x00)
            {
                fin--;
            }

            return fin >= 4 && d[fin - 2] == 0xFF && d[fin - 1] == 0xD9;
        }

        if (d.Length >= 6 && d[0] == (byte)'B' && d[1] == (byte)'M')
        {
            uint declarado = BitConverter.ToUInt32(d, 2);
            return declarado == 0 || declarado <= (uint)d.Length;
        }

        return true;
    }

    private static bool PngCompleto(byte[] d)
    {
        int pos = 8;
        while (pos + 12 <= d.Length)
        {
            uint longitud = ReadUInt32BigEndian(d, pos);
            if (longitud > int.MaxValue - 12 || pos + 12L + longitud > d.Length)
            {
                return false;
            }

            uint esperado = ReadUInt32BigEndian(d, pos + 8 + (int)longitud);
            if (Crc32(d, pos + 4, 4 + (int)longitud) != esperado)
            {
                return false;
            }

            bool esIend = d[pos + 4] == (byte)'I' && d[pos + 5] == (byte)'E' && d[pos + 6] == (byte)'N' && d[pos + 7] == (byte)'D';
            if (esIend)
            {
                return true;
            }

            pos += 12 + (int)longitud;
        }

        return false;
    }

    private static uint ReadUInt32BigEndian(byte[] d, int pos) =>
        ((uint)d[pos] << 24) | ((uint)d[pos + 1] << 16) | ((uint)d[pos + 2] << 8) | d[pos + 3];

    private static readonly uint[] TablaCrc = CrearTablaCrc();

    private static uint[] CrearTablaCrc()
    {
        var tabla = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            tabla[n] = c;
        }

        return tabla;
    }

    private static uint Crc32(byte[] d, int inicio, int cantidad)
    {
        uint c = 0xFFFFFFFFu;
        for (int i = inicio; i < inicio + cantidad; i++)
        {
            c = TablaCrc[(c ^ d[i]) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }
}
