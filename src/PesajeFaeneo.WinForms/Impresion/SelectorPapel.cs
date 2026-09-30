using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.WinForms.Impresion;

internal sealed record PapelDisponible(string Nombre, int AnchoCentPulg, int AltoCentPulg, bool EsPersonalizado = false);
internal sealed record EleccionPapel(PapelDisponible Papel, bool Horizontal);

internal static class SelectorPapel
{
    public const int Tolerancia = 15; // centésimas de pulgada (~4 mm)

    public static EleccionPapel Elegir(
        FormatoBoleta formato,
        string? nombreConfigurado,
        IReadOnlyList<PapelDisponible> disponibles,
        float altoContenidoMm)
    {
        ArgumentNullException.ThrowIfNull(disponibles);

        // 1. Configurado presente en disponibles
        if (!string.IsNullOrWhiteSpace(nombreConfigurado))
        {
            var encontrado = disponibles.FirstOrDefault(p => string.Equals(p.Nombre, nombreConfigurado, StringComparison.OrdinalIgnoreCase));
            if (encontrado is not null)
            {
                var horizontal = formato == FormatoBoleta.MediaCarta &&
                                 encontrado.AnchoCentPulg < 850 - Tolerancia &&
                                 encontrado.AltoCentPulg >= 850 - Tolerancia;
                return new EleccionPapel(encontrado, horizontal);
            }
        }

        // 2. MediaCarta automático
        if (formato == FormatoBoleta.MediaCarta)
        {
            var semMediaCarta = disponibles.FirstOrDefault(p =>
                (Math.Abs(p.AnchoCentPulg - 550) <= Tolerancia && Math.Abs(p.AltoCentPulg - 850) <= Tolerancia) ||
                (Math.Abs(p.AnchoCentPulg - 850) <= Tolerancia && Math.Abs(p.AltoCentPulg - 550) <= Tolerancia));

            if (semMediaCarta is not null)
            {
                return new EleccionPapel(semMediaCarta, semMediaCarta.AnchoCentPulg < semMediaCarta.AltoCentPulg);
            }

            var carta = disponibles.FirstOrDefault(p =>
                Math.Abs(p.AnchoCentPulg - 850) <= Tolerancia && Math.Abs(p.AltoCentPulg - 1100) <= Tolerancia);

            if (carta is not null)
            {
                return new EleccionPapel(carta, false);
            }

            return new EleccionPapel(new PapelDisponible("Media carta (personalizado)", 850, 550, EsPersonalizado: true), false);
        }

        // 3. Ticket80mm automático
        var ancho315 = disponibles.FirstOrDefault(p => Math.Abs(p.AnchoCentPulg - 315) <= Tolerancia);
        if (ancho315 is not null)
        {
            return new EleccionPapel(ancho315, false);
        }

        var altoCentPulg = (int)Math.Ceiling(altoContenidoMm / 0.254f);
        return new EleccionPapel(new PapelDisponible("Ticket 80 mm (personalizado)", 315, altoCentPulg, EsPersonalizado: true), false);
    }
}
