using System.Text;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Balanza;

internal sealed class SeparadorTramas
{
    private readonly StringBuilder _acumulador = new();
    private readonly bool _espacioEsDelimitador;
    private readonly bool _igualIniciaTrama;
    private bool _descartandoHastaDelimitador;

    public SeparadorTramas(DelimitadorTrama delimitador = DelimitadorTrama.FinDeLinea)
    {
        // SignoIgual: el RX8 Accurex transmite "=000000 " sin CR/LF y, en ráfagas, encadena tramas sin
        // espacio ("=650000-=381000-…"); lo único constante es el '=' al inicio de cada trama.
        _igualIniciaTrama = delimitador == DelimitadorTrama.SignoIgual;
        _espacioEsDelimitador = delimitador is DelimitadorTrama.Espacio or DelimitadorTrama.SignoIgual;
    }

    public int TramasDescartadas { get; private set; }

    public IReadOnlyList<string> Agregar(byte[] buffer, int cantidad)
    {
        if (buffer == null || cantidad <= 0)
        {
            return Array.Empty<string>();
        }

        var tramas = new List<string>();
        var texto = Encoding.Latin1.GetString(buffer, 0, cantidad);

        foreach (var ch in texto)
        {
            if (ch == '\x02') // STX: descarta lo acumulado
            {
                _acumulador.Clear();
                _descartandoHastaDelimitador = false;
                continue;
            }

            if (_igualIniciaTrama && ch == '=')
            {
                if (!_descartandoHastaDelimitador && _acumulador.Length > 0)
                {
                    tramas.Add(_acumulador.ToString());
                }
                _acumulador.Clear();
                _descartandoHastaDelimitador = false;
                _acumulador.Append(ch);
                continue;
            }

            if (ch == '\r' || ch == '\n' || ch == '\x03' || (_espacioEsDelimitador && ch == ' '))
            {
                if (_descartandoHastaDelimitador)
                {
                    _descartandoHastaDelimitador = false;
                    _acumulador.Clear();
                    continue;
                }

                if (_acumulador.Length > 0)
                {
                    tramas.Add(_acumulador.ToString());
                    _acumulador.Clear();
                }
                continue;
            }

            if (_descartandoHastaDelimitador)
            {
                continue;
            }

            _acumulador.Append(ch);

            if (_acumulador.Length > ReglasBalanza.LongitudMaximaTrama)
            {
                TramasDescartadas++;
                _acumulador.Clear();
                _descartandoHastaDelimitador = true;
            }
        }

        return tramas;
    }

    public void Reiniciar()
    {
        _acumulador.Clear();
        _descartandoHastaDelimitador = false;
    }
}
