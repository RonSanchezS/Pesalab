using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Pesajes;

public static class ValidadorPesaje
{
    public static (RegistrarPesaje Normalizado, IReadOnlyList<ErrorValidacion> Errores) Validar(RegistrarPesaje dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var direccionNormalizada = ValidadorCatalogos.NormalizarMayusculas(dto.Direccion);
        var colorRazaNormalizado = ValidadorCatalogos.NormalizarMayusculas(dto.ColorRaza);
        var observacionNormalizada = ValidadorCatalogos.NormalizarMayusculas(dto.Observacion);
        var sexoNormalizado = char.ToUpperInvariant(dto.Sexo);
        var pesoIzqRedondeado = ReglasPesaje.Redondear(dto.PesoIzquierdo);
        var pesoDerRedondeado = ReglasPesaje.Redondear(dto.PesoDerecho);

        var normalizado = dto with
        {
            Direccion = direccionNormalizada,
            ColorRaza = colorRazaNormalizado,
            Observacion = observacionNormalizada,
            Sexo = sexoNormalizado,
            PesoIzquierdo = pesoIzqRedondeado,
            PesoDerecho = pesoDerRedondeado
        };

        var errores = new List<ErrorValidacion>();

        if (dto.GanaderoId <= 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Ganadero, "Seleccione el ganadero."));
        }

        if (dto.ClienteId <= 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Cliente, "Seleccione el cliente."));
        }

        if (dto.Secuencia < ReglasPesaje.SecuenciaMinima || dto.Secuencia > ReglasPesaje.SecuenciaMaxima)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Secuencia, "La secuencia debe estar entre 1 y 9999."));
        }

        if (!ReglasPesaje.EsSexoValido(sexoNormalizado))
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Sexo, "Seleccione el sexo (M o H)."));
        }

        if (direccionNormalizada.Length > ReglasPesaje.LongitudMaximaDireccion)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Direccion, "La dirección no puede superar 150 caracteres."));
        }

        if (colorRazaNormalizado.Length > ReglasPesaje.LongitudMaximaColorRaza)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.ColorRaza, "Color/raza no puede superar 30 caracteres."));
        }

        if (observacionNormalizada.Length > ReglasPesaje.LongitudMaximaObservacion)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Observacion, "La observación no puede superar 250 caracteres."));
        }

        var erroresPesos = ReglasPesaje.ValidarPesos(dto.TipoDesposte, pesoIzqRedondeado, pesoDerRedondeado);
        errores.AddRange(erroresPesos);

        return (normalizado, errores);
    }
}
