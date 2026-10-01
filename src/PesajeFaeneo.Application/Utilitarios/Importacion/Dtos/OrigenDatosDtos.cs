namespace PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

public enum FormatoOrigen
{
    Csv = 1,
    Dbf = 2,
    Access = 3
}

public sealed record SolicitudLectura(string Ruta, FormatoOrigen Formato, string? Tabla, int? PaginaCodigos);

/// <summary>Numero = fila del archivo (CSV: 2 = primera fila de datos; DBF: número de registro; Access: 1..n).</summary>
public sealed record FilaOrigen(int Numero, IReadOnlyList<string?> Valores);

public sealed record TablaOrigen(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<FilaOrigen> Filas,
    string Huella,
    string Codificacion,
    IReadOnlyList<string> Advertencias);

public static class FormatosOrigen
{
    /// <summary>.csv/.txt → Csv; .dbf → Dbf; .mdb/.accdb → Access; otra → null (ignora mayúsculas).</summary>
    public static FormatoOrigen? DesdeExtension(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return null;
        }

        var ext = Path.GetExtension(ruta);
        return ext.ToLowerInvariant() switch
        {
            ".csv" or ".txt" => FormatoOrigen.Csv,
            ".dbf" => FormatoOrigen.Dbf,
            ".mdb" or ".accdb" => FormatoOrigen.Access,
            _ => null
        };
    }
}
