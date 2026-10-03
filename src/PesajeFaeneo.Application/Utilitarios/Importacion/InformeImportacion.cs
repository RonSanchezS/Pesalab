using System.Globalization;
using System.Text;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class InformeImportacion
{
    public static byte[] GenerarCsv(ResultadoImportacion r, string nombreArchivoOrigen)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(nombreArchivoOrigen);

        var sb = new StringBuilder();

        var resultadoTexto = r.EsSimulacro ? "Simulacro" : "Importación";

        sb.Append("Archivo;").Append(Escapar(nombreArchivoOrigen)).Append("\r\n");
        sb.Append("Tipo;").Append(r.Tipo).Append("\r\n");
        sb.Append("Resultado;").Append(resultadoTexto).Append("\r\n");
        sb.Append("Filas leídas;").Append(r.FilasLeidas.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("A importar;").Append(r.FilasAImportar.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Insertadas;").Append(r.Insertadas.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Ya existentes (omitidas);").Append(r.FilasOmitidas.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Con errores (no importadas);").Append(r.FilasConError.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Con advertencias;").Append(r.FilasConAdvertencia.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("\r\n");

        sb.Append("Fila;Nivel;Campo;Mensaje\r\n");

        foreach (var inc in r.Incidencias)
        {
            sb.Append(inc.Fila.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(inc.Nivel).Append(';')
              .Append(Escapar(inc.Campo)).Append(';')
              .Append(Escapar(inc.Mensaje)).Append("\r\n");
        }

        if (r.IncidenciasTruncadas)
        {
            sb.Append($"(Se muestran las primeras {r.Incidencias.Count} incidencias)\r\n");
        }

        var bytesTexto = Encoding.UTF8.GetBytes(sb.ToString());
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var resultado = new byte[bom.Length + bytesTexto.Length];
        Buffer.BlockCopy(bom, 0, resultado, 0, bom.Length);
        Buffer.BlockCopy(bytesTexto, 0, resultado, bom.Length, bytesTexto.Length);

        return resultado;
    }

    private static string Escapar(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        var s = valor;
        if (s.StartsWith('=') || s.StartsWith('+') || s.StartsWith('-') || s.StartsWith('@'))
        {
            s = "'" + s;
        }

        if (s.Contains(';') || s.Contains('"') || s.Contains('\r') || s.Contains('\n'))
        {
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        return s;
    }
}
