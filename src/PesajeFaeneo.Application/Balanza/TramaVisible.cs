using System.Text;

namespace PesajeFaeneo.Application.Balanza;

public static class TramaVisible
{
    public static string Formatear(string trama)
    {
        if (string.IsNullOrEmpty(trama))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trama.Length);
        foreach (var ch in trama)
        {
            if (char.IsControl(ch))
            {
                sb.Append($"<0x{(int)ch:X2}>");
            }
            else
            {
                sb.Append(ch);
            }
        }

        var resultado = sb.ToString();
        if (resultado.Length > ReglasBalanza.LongitudMaximaTrama)
        {
            resultado = resultado[..ReglasBalanza.LongitudMaximaTrama];
        }

        return resultado;
    }
}
