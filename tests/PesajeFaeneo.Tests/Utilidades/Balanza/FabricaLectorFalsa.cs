using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Balanza;

public class FabricaLectorFalsa : IFabricaLectorBalanza
{
    private readonly object _lock = new();
    private readonly Queue<LectorBalanzaFalso> _cola = new();
    private readonly List<ConfiguracionBalanzaDto> _configuracionesRecibidas = new();
    private readonly List<LectorBalanzaFalso> _creados = new();

    public void Encolar(LectorBalanzaFalso lector)
    {
        lock (_lock)
        {
            _cola.Enqueue(lector);
        }
    }

    public IReadOnlyList<ConfiguracionBalanzaDto> ConfiguracionesRecibidas
    {
        get
        {
            lock (_lock)
            {
                return _configuracionesRecibidas.ToList();
            }
        }
    }

    public IReadOnlyList<LectorBalanzaFalso> Creados
    {
        get
        {
            lock (_lock)
            {
                return _creados.ToList();
            }
        }
    }

    public IScaleReader Crear(ConfiguracionBalanzaDto configuracion)
    {
        lock (_lock)
        {
            _configuracionesRecibidas.Add(configuracion);
            var lector = _cola.Count > 0 ? _cola.Dequeue() : new LectorBalanzaFalso();
            _creados.Add(lector);
            return lector;
        }
    }
}
