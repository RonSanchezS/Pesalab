using System.Globalization;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Boletas;

public static class ValidadorConfiguracionImpresion
{
    /// <summary>
    /// Normaliza (trim; vacío o solo espacios → null en impresora y papel; desplazamientos redondeados
    /// a 1 decimal AwayFromZero) y devuelve la configuración normalizada + errores.
    /// </summary>
    public static (ConfiguracionImpresion Normalizada, IReadOnlyList<ErrorValidacion> Errores) Validar(ConfiguracionImpresion c)
    {
        ArgumentNullException.ThrowIfNull(c);

        var impresora = string.IsNullOrWhiteSpace(c.NombreImpresora) ? null : c.NombreImpresora.Trim();
        var papel = string.IsNullOrWhiteSpace(c.NombrePapel) ? null : c.NombrePapel.Trim();
        var despX = Math.Round(c.DesplazamientoXMm, 1, MidpointRounding.AwayFromZero);
        var despY = Math.Round(c.DesplazamientoYMm, 1, MidpointRounding.AwayFromZero);

        var normalizada = new ConfiguracionImpresion(
            impresora,
            c.Formato,
            papel,
            c.Copias,
            c.ImprimirAlGrabar,
            despX,
            despY);

        var errores = new List<ErrorValidacion>();

        if (!Enum.IsDefined(normalizada.Formato))
        {
            errores.Add(new ErrorValidacion(CamposImpresion.Formato, "Seleccione el formato de boleta."));
        }

        if (normalizada.Copias < ReglasImpresion.CopiasMinimas || normalizada.Copias > ReglasImpresion.CopiasMaximas)
        {
            errores.Add(new ErrorValidacion(
                CamposImpresion.Copias,
                $"Las copias deben estar entre {ReglasImpresion.CopiasMinimas.ToString("0", CultureInfo.InvariantCulture)} y {ReglasImpresion.CopiasMaximas.ToString("0", CultureInfo.InvariantCulture)}."));
        }

        if (Math.Abs(normalizada.DesplazamientoXMm) > ReglasImpresion.DesplazamientoMaximoMm)
        {
            errores.Add(new ErrorValidacion(
                CamposImpresion.DesplazamientoX,
                $"El desplazamiento horizontal debe estar entre -{ReglasImpresion.DesplazamientoMaximoMm.ToString("0", CultureInfo.InvariantCulture)} y {ReglasImpresion.DesplazamientoMaximoMm.ToString("0", CultureInfo.InvariantCulture)} mm."));
        }

        if (Math.Abs(normalizada.DesplazamientoYMm) > ReglasImpresion.DesplazamientoMaximoMm)
        {
            errores.Add(new ErrorValidacion(
                CamposImpresion.DesplazamientoY,
                $"El desplazamiento vertical debe estar entre -{ReglasImpresion.DesplazamientoMaximoMm.ToString("0", CultureInfo.InvariantCulture)} y {ReglasImpresion.DesplazamientoMaximoMm.ToString("0", CultureInfo.InvariantCulture)} mm."));
        }

        if (normalizada.NombreImpresora is not null && normalizada.NombreImpresora.Length > ReglasImpresion.LongitudMaximaNombreImpresora)
        {
            errores.Add(new ErrorValidacion(CamposImpresion.Impresora, "El nombre de la impresora es demasiado largo."));
        }

        if (normalizada.NombrePapel is not null && normalizada.NombrePapel.Length > ReglasImpresion.LongitudMaximaNombrePapel)
        {
            errores.Add(new ErrorValidacion(CamposImpresion.Papel, "El nombre del papel es demasiado largo."));
        }

        return (normalizada, errores);
    }
}
