using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Balanza;

public static class DescripcionConexion
{
    public static string Crear(ConfiguracionBalanzaDto c)
    {
        ArgumentNullException.ThrowIfNull(c);

        return c.TipoConexion switch
        {
            TipoConexion.Serial => $"{c.PuertoCom} {c.BaudRate} {c.DataBits}{LetraParidad(c.Paridad)}{BitsParadaTexto(c.BitsParada)}",
            TipoConexion.Tcp => $"{c.HostTcp}:{c.PuertoTcp}",
            TipoConexion.Simulador => "Simulador",
            _ => "Desconocido"
        };
    }

    private static string LetraParidad(ParidadSerial paridad) => paridad switch
    {
        ParidadSerial.None => "N",
        ParidadSerial.Odd => "O",
        ParidadSerial.Even => "E",
        ParidadSerial.Mark => "M",
        ParidadSerial.Space => "S",
        _ => "?"
    };

    private static string BitsParadaTexto(BitsParada bitsParada) => bitsParada switch
    {
        BitsParada.One => "1",
        BitsParada.Two => "2",
        BitsParada.OnePointFive => "1.5",
        _ => "?"
    };
}
