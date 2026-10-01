using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Pesajes;

public static class ValidadorGestionPesajes
{
    public static (EditarPesaje Normalizado, IReadOnlyList<ErrorValidacion> Errores) ValidarEdicion(EditarPesaje dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var direccion = ValidadorCatalogos.NormalizarMayusculas(dto.Direccion);
        var colorRaza = ValidadorCatalogos.NormalizarMayusculas(dto.ColorRaza);
        var observacion = ValidadorCatalogos.NormalizarMayusculas(dto.Observacion);
        var motivo = ValidadorCatalogos.NormalizarMayusculas(dto.Motivo);
        var sexo = char.ToUpperInvariant(dto.Sexo);

        var normalizado = dto with
        {
            Direccion = direccion,
            ColorRaza = colorRaza,
            Observacion = observacion,
            Motivo = motivo,
            Sexo = sexo
        };

        var errores = new List<ErrorValidacion>();

        if (normalizado.GanaderoId <= 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Ganadero, "Seleccione el ganadero."));
        }

        if (normalizado.ClienteId <= 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Cliente, "Seleccione el cliente."));
        }

        if (normalizado.Secuencia < ReglasPesaje.SecuenciaMinima || normalizado.Secuencia > ReglasPesaje.SecuenciaMaxima)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Secuencia, "La secuencia debe estar entre 1 y 9999."));
        }

        if (!ReglasPesaje.EsSexoValido(normalizado.Sexo))
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Sexo, "Seleccione el sexo (M o H)."));
        }

        if (normalizado.Direccion.Length > ReglasPesaje.LongitudMaximaDireccion)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Direccion, "La dirección no puede superar 150 caracteres."));
        }

        if (normalizado.ColorRaza.Length > ReglasPesaje.LongitudMaximaColorRaza)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.ColorRaza, "Color/raza no puede superar 30 caracteres."));
        }

        if (normalizado.Observacion.Length > ReglasPesaje.LongitudMaximaObservacion)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Observacion, "La observación no puede superar 250 caracteres."));
        }

        if (normalizado.Motivo.Length > ReglasGestionPesajes.MotivoEdicionMaximo)
        {
            errores.Add(new ErrorValidacion(CamposGestionPesajes.Motivo, "El motivo no puede superar 100 caracteres."));
        }

        return (normalizado, errores.AsReadOnly());
    }

    public static (AnularPesaje Normalizado, IReadOnlyList<ErrorValidacion> Errores) ValidarAnulacion(AnularPesaje dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var motivo = ValidadorCatalogos.NormalizarMayusculas(dto.Motivo);
        var normalizado = dto with { Motivo = motivo };

        var errores = new List<ErrorValidacion>();

        if (string.IsNullOrEmpty(motivo) || motivo.Length < ReglasGestionPesajes.MotivoAnulacionMinimo)
        {
            errores.Add(new ErrorValidacion(CamposGestionPesajes.Motivo, "Ingrese el motivo de la anulación (mínimo 5 caracteres)."));
        }
        else if (motivo.Length > ReglasGestionPesajes.MotivoAnulacionMaximo)
        {
            errores.Add(new ErrorValidacion(CamposGestionPesajes.Motivo, "El motivo no puede superar 100 caracteres."));
        }

        return (normalizado, errores.AsReadOnly());
    }
}
