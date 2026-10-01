using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Application.Reportes;

public sealed record ColumnaReporte(
    string Clave,          // identificador estable, p. ej. "Item"
    string Titulo,         // encabezado impreso (puede contener "\n")
    string? Grupo,         // encabezado agrupador de la fila superior (solo "PESO KG."); null = ocupa las dos filas
    float AnchoPt,         // 0 = columna relativa (ocupa el resto)
    bool AlineadaDerecha);

public static class ColumnasReporte
{
    private static readonly IReadOnlyList<ColumnaReporte> ColumnasNormales = new List<ColumnaReporte>
    {
        new("Item", "Item", null, 24f, false),
        new("Numero", "N° Bol.", null, 40f, false),
        new("Fecha", "Fecha", null, 44f, false),
        new("Secuencia", "N° Res", null, 26f, false),
        new("Marca", "Marca", null, 30f, false),
        new("Cliente", "Cliente", null, 30f, false),
        new("Direccion", "Dirección", null, 0f, false),
        new("Izquierdo", "IZQ.", "PESO KG.", 34f, true),
        new("Derecho", "DER.", "PESO KG.", 34f, true),
        new("Total", "TOTAL\nNETO", null, 40f, true),
        new("Desposte", "Tipo Corte", null, 58f, false)
    }.AsReadOnly();

    private static readonly IReadOnlyList<ColumnaReporte> ColumnasAnuladas = new List<ColumnaReporte>
    {
        new("Item", "Item", null, 24f, false),
        new("Numero", "N° Bol.", null, 40f, false),
        new("Fecha", "Fecha", null, 44f, false),
        new("Marca", "Marca", null, 30f, false),
        new("Cliente", "Cliente", null, 30f, false),
        new("Desposte", "Tipo Corte", null, 58f, false),
        new("Total", "TOTAL\nNETO", null, 40f, true),
        new("AnuladaEl", "Anulada el", null, 60f, false),
        new("AnuladaPor", "Anulada por", null, 60f, false),
        new("Motivo", "Motivo", null, 0f, false)
    }.AsReadOnly();

    public static IReadOnlyList<ColumnaReporte> Para(bool anuladas) =>
        anuladas ? ColumnasAnuladas : ColumnasNormales;

    public static IReadOnlyList<string> Textos(FilaReporte f, bool anuladas)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (!anuladas)
        {
            return new string[]
            {
                TextosReporte.Entero(f.Item),
                TextosReporte.NumeroBoleta(f),
                TextosReporte.Fecha(f.FechaHoraLocal),
                f.Secuencia.ToString(TextosBoleta.Cultura),
                f.MarcaGanadero,
                f.CodigoCliente.ToString(TextosBoleta.Cultura),
                f.Direccion,
                TextosReporte.Kg(f.PesoIzquierdo),
                TextosReporte.Kg(f.PesoDerecho),
                TextosReporte.Kg(f.PesoTotal),
                TextosBoleta.Desposte(f.TipoDesposte)
            };
        }

        return new string[]
        {
            TextosReporte.Entero(f.Item),
            TextosReporte.NumeroBoleta(f),
            TextosReporte.Fecha(f.FechaHoraLocal),
            f.MarcaGanadero,
            f.CodigoCliente.ToString(TextosBoleta.Cultura),
            TextosBoleta.Desposte(f.TipoDesposte),
            TextosReporte.Kg(f.PesoTotal),
            f.AnuladoEnLocal is { } a ? TextosReporte.FechaHora(a) : string.Empty,
            f.AnuladoPor ?? string.Empty,
            f.MotivoAnulacion ?? string.Empty
        };
    }
}
