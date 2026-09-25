using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Balanza;

public static class MapeoBalanza
{
    public static ConfiguracionBalanzaDto ADto(ConfiguracionBalanza entidad)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        return new ConfiguracionBalanzaDto(
            entidad.TipoConexion,
            entidad.PuertoCom,
            entidad.BaudRate,
            entidad.DataBits,
            entidad.Paridad,
            entidad.BitsParada,
            entidad.HostTcp,
            entidad.PuertoTcp,
            entidad.PatronRegex,
            entidad.LecturasParaEstable,
            entidad.ToleranciaKg,
            entidad.TimeoutMs,
            entidad.DelimitadorTrama,
            entidad.UnidadPeso);
    }

    public static void Aplicar(ConfiguracionBalanzaDto dto, ConfiguracionBalanza entidad)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entidad);

        entidad.TipoConexion = dto.TipoConexion;
        entidad.PuertoCom = dto.PuertoCom;
        entidad.BaudRate = dto.BaudRate;
        entidad.DataBits = dto.DataBits;
        entidad.Paridad = dto.Paridad;
        entidad.BitsParada = dto.BitsParada;
        entidad.HostTcp = dto.HostTcp;
        entidad.PuertoTcp = dto.PuertoTcp;
        entidad.PatronRegex = dto.PatronRegex;
        entidad.LecturasParaEstable = dto.LecturasParaEstable;
        entidad.ToleranciaKg = dto.ToleranciaKg;
        entidad.TimeoutMs = dto.TimeoutMs;
        entidad.DelimitadorTrama = dto.DelimitadorTrama;
        entidad.UnidadPeso = dto.UnidadPeso;
    }
}
