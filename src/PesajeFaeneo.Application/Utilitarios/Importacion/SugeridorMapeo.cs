using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class SugeridorMapeo
{
    public static IReadOnlyDictionary<string, int?> Sugerir(TipoImportacion tipo, IReadOnlyList<string> columnas)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var campos = CamposImportacion.De(tipo);
        var resultado = new Dictionary<string, int?>(campos.Count);
        var columnasAsignadas = new HashSet<int>();

        // Precalcular las claves normalizadas de las columnas
        var clavesColumnas = new string[columnas.Count];
        for (int i = 0; i < columnas.Count; i++)
        {
            clavesColumnas[i] = ConversoresImportacion.ClaveColumna(columnas[i]);
        }

        foreach (var campo in campos)
        {
            int? indiceAsignado = null;

            for (int i = 0; i < columnas.Count; i++)
            {
                if (columnasAsignadas.Contains(i))
                {
                    continue;
                }

                var claveCol = clavesColumnas[i];
                if (campo.Alias.Contains(claveCol, StringComparer.OrdinalIgnoreCase))
                {
                    indiceAsignado = i;
                    columnasAsignadas.Add(i);
                    break;
                }
            }

            resultado[campo.Clave] = indiceAsignado;
        }

        return resultado;
    }
}
