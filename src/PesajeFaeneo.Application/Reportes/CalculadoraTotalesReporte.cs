using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Reportes;

public static class CalculadoraTotalesReporte
{
    public static TotalesReporte Calcular(IReadOnlyList<FilaReporte> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        int completas = 0;
        int medias = 0;
        decimal reses = 0m;
        decimal izq = 0m;
        decimal der = 0m;
        decimal total = 0m;
        int simuladas = 0;

        foreach (var f in filas)
        {
            if (f.TipoDesposte == TipoDesposte.ResCompleta)
            {
                completas++;
            }
            else if (f.TipoDesposte == TipoDesposte.MediaRes)
            {
                medias++;
            }

            reses += ReglasReportes.Reses(f.TipoDesposte);
            izq += f.PesoIzquierdo;
            der += f.PesoDerecho;
            total += f.PesoTotal;

            if (f.Origen == OrigenPeso.Simulador)
            {
                simuladas++;
            }
        }

        return new TotalesReporte(filas.Count, completas, medias, reses, izq, der, total, simuladas);
    }
}
