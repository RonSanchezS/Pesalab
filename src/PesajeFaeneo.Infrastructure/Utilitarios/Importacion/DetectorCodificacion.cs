using System.Text;
using PesajeFaeneo.Application.Utilitarios.Importacion;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

internal static class DetectorCodificacion
{
    static DetectorCodificacion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static (Encoding Codificacion, int SaltoBom, string Nombre) Detectar(byte[] datos, int? forzada)
    {
        if (forzada.HasValue)
        {
            if (!ReglasImportacion.PaginasPermitidas.Contains(forzada.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(forzada), "Codificación no permitida.");
            }

            if (forzada.Value == 65001)
            {
                if (datos.Length >= 3 && datos[0] == 0xEF && datos[1] == 0xBB && datos[2] == 0xBF)
                {
                    return (Encoding.UTF8, 3, "UTF-8");
                }

                return (Encoding.UTF8, 0, "UTF-8");
            }

            var encForzada = Encoding.GetEncoding(forzada.Value);
            return (encForzada, 0, NombreDe(forzada.Value));
        }

        // Detección automática por BOM
        if (datos.Length >= 3 && datos[0] == 0xEF && datos[1] == 0xBB && datos[2] == 0xBF)
        {
            return (Encoding.UTF8, 3, "UTF-8");
        }

        if (datos.Length >= 2 && datos[0] == 0xFF && datos[1] == 0xFE)
        {
            return (Encoding.Unicode, 2, "UTF-16");
        }

        if (datos.Length >= 2 && datos[0] == 0xFE && datos[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2, "UTF-16");
        }

        // Intento estricto UTF-8 sin BOM
        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = utf8Strict.GetString(datos);
            return (Encoding.UTF8, 0, "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(1252), 0, "Windows-1252");
        }
    }

    public static string NombreDe(int pagina)
    {
        return pagina switch
        {
            65001 => "UTF-8",
            1252 => "Windows-1252",
            850 => "DOS 850",
            437 => "DOS 437",
            1200 or 1201 => "UTF-16",
            _ => $"Página {pagina}"
        };
    }
}
