using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Parametros;

public static class ValidadorParametros
{
    public static IReadOnlyList<ErrorValidacion> Validar(ActualizarParametros dtoNormalizado)
    {
        var errores = new List<ErrorValidacion>();

        if (string.IsNullOrEmpty(dtoNormalizado.NombreEmpresa) ||
            dtoNormalizado.NombreEmpresa.Length < ReglasParametros.LongitudMinimaNombreEmpresa ||
            dtoNormalizado.NombreEmpresa.Length > ReglasParametros.LongitudMaximaNombreEmpresa)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.NombreEmpresa,
                "Ingrese el nombre de la empresa (3 a 100 caracteres)."));
        }

        if (dtoNormalizado.Direccion?.Length > ReglasParametros.LongitudMaximaDireccion)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.Direccion,
                $"No puede superar {ReglasParametros.LongitudMaximaDireccion} caracteres."));
        }

        if (dtoNormalizado.Telefono?.Length > ReglasParametros.LongitudMaximaTelefono)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.Telefono,
                $"No puede superar {ReglasParametros.LongitudMaximaTelefono} caracteres."));
        }

        if (dtoNormalizado.Ciudad?.Length > ReglasParametros.LongitudMaximaCiudad)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.Ciudad,
                $"No puede superar {ReglasParametros.LongitudMaximaCiudad} caracteres."));
        }

        if (dtoNormalizado.RegistroSanitario?.Length > ReglasParametros.LongitudMaximaRegistroSanitario)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.RegistroSanitario,
                $"No puede superar {ReglasParametros.LongitudMaximaRegistroSanitario} caracteres."));
        }

        if (dtoNormalizado.TelefonosReclamos?.Length > ReglasParametros.LongitudMaximaTelefonosReclamos)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.TelefonosReclamos,
                $"No puede superar {ReglasParametros.LongitudMaximaTelefonosReclamos} caracteres."));
        }

        if (dtoNormalizado.ObservacionPorDefecto?.Length > ReglasParametros.LongitudMaximaObservacionPorDefecto)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.ObservacionPorDefecto,
                $"No puede superar {ReglasParametros.LongitudMaximaObservacionPorDefecto} caracteres."));
        }

        if (dtoNormalizado.SiguienteNumeroBoleta < ReglasParametros.SiguienteNumeroBoletaMinimo ||
            dtoNormalizado.SiguienteNumeroBoleta > ReglasParametros.SiguienteNumeroBoletaMaximo)
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.SiguienteNumeroBoleta,
                "El número debe estar entre 1 y 999999999."));
        }

        if (dtoNormalizado.AccionLogo == AccionLogo.Reemplazar && string.IsNullOrWhiteSpace(dtoNormalizado.RutaLogoOrigen))
        {
            errores.Add(new ErrorValidacion(
                CamposParametros.Logo,
                "Seleccione el archivo del logo."));
        }

        return errores.AsReadOnly();
    }
}
