using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Balanza;

public static class ValidadorConfiguracionBalanza
{
    public static ConfiguracionBalanzaDto Normalizar(ConfiguracionBalanzaDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto with
        {
            PuertoCom = dto.PuertoCom?.Trim().ToUpperInvariant() ?? string.Empty,
            HostTcp = dto.HostTcp?.Trim() ?? string.Empty
            // PatronRegex: SIN cambios
        };
    }

    public static IReadOnlyList<ErrorValidacion> Validar(ConfiguracionBalanzaDto dtoNormalizado)
    {
        ArgumentNullException.ThrowIfNull(dtoNormalizado);

        var errores = new List<ErrorValidacion>();

        if (!Enum.IsDefined(typeof(TipoConexion), dtoNormalizado.TipoConexion))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.TipoConexion, "Seleccione el tipo de conexión."));
        }

        if (string.IsNullOrEmpty(dtoNormalizado.PuertoCom) ||
            !Regex.IsMatch(dtoNormalizado.PuertoCom, ReglasBalanza.PatronPuertoCom))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.PuertoCom, "Ingrese un puerto válido (COM1 a COM999)."));
        }

        if (!ReglasBalanza.BaudiosPermitidos.Contains(dtoNormalizado.BaudRate))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.BaudRate, "Seleccione una velocidad válida."));
        }

        if (!ReglasBalanza.BitsDatosPermitidos.Contains(dtoNormalizado.DataBits))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.DataBits, "Los bits de datos deben ser 7 u 8."));
        }

        if (!Enum.IsDefined(typeof(ParidadSerial), dtoNormalizado.Paridad))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.Paridad, "Seleccione la paridad."));
        }

        if (!ReglasBalanza.BitsParadaPermitidos.Contains(dtoNormalizado.BitsParada))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.BitsParada, "Los bits de parada deben ser 1 o 2."));
        }

        var hostType = string.IsNullOrEmpty(dtoNormalizado.HostTcp) ? UriHostNameType.Unknown : Uri.CheckHostName(dtoNormalizado.HostTcp);
        if (string.IsNullOrWhiteSpace(dtoNormalizado.HostTcp) ||
            dtoNormalizado.HostTcp.Length > ReglasBalanza.LongitudMaximaHost ||
            (hostType != UriHostNameType.Dns && hostType != UriHostNameType.IPv4 && hostType != UriHostNameType.IPv6))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.HostTcp, "Ingrese una dirección IP o nombre de equipo válido."));
        }

        if (dtoNormalizado.PuertoTcp < ReglasBalanza.PuertoTcpMinimo || dtoNormalizado.PuertoTcp > ReglasBalanza.PuertoTcpMaximo)
        {
            errores.Add(new ErrorValidacion(CamposBalanza.PuertoTcp, "El puerto TCP debe estar entre 1 y 65535."));
        }

        var errorPatron = ParserTramas.ValidarPatron(dtoNormalizado.PatronRegex);
        if (errorPatron != null)
        {
            errores.Add(new ErrorValidacion(CamposBalanza.PatronRegex, errorPatron));
        }

        if (dtoNormalizado.LecturasParaEstable < ReglasBalanza.LecturasMinimas || dtoNormalizado.LecturasParaEstable > ReglasBalanza.LecturasMaximas)
        {
            errores.Add(new ErrorValidacion(CamposBalanza.LecturasParaEstable, "Las lecturas para estabilidad deben estar entre 1 y 50."));
        }

        if (dtoNormalizado.ToleranciaKg < ReglasBalanza.ToleranciaMinimaKg ||
            dtoNormalizado.ToleranciaKg > ReglasBalanza.ToleranciaMaximaKg ||
            dtoNormalizado.ToleranciaKg != Math.Round(dtoNormalizado.ToleranciaKg, 1))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.ToleranciaKg, "La tolerancia debe estar entre 0,0 y 10,0 kg (un decimal)."));
        }

        if (dtoNormalizado.TimeoutMs < ReglasBalanza.TimeoutMinimoMs || dtoNormalizado.TimeoutMs > ReglasBalanza.TimeoutMaximoMs)
        {
            errores.Add(new ErrorValidacion(CamposBalanza.TimeoutMs, "El tiempo sin datos debe estar entre 500 y 60000 ms."));
        }

        if (!Enum.IsDefined(typeof(DelimitadorTrama), dtoNormalizado.DelimitadorTrama))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.DelimitadorTrama, "Seleccione cómo se separan las tramas."));
        }

        if (!Enum.IsDefined(typeof(UnidadPesoTrama), dtoNormalizado.UnidadPeso))
        {
            errores.Add(new ErrorValidacion(CamposBalanza.UnidadPeso, "Seleccione la unidad en que transmite la balanza."));
        }

        return errores;
    }
}
