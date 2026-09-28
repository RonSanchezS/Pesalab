using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class SimuladorBalanzaFalso : ISimuladorBalanza
{
    public decimal PesoObjetivoKg { get; private set; }
    public bool DesconexionSimulada { get; set; }

    public void EstablecerPesoObjetivo(decimal pesoKg)
    {
        PesoObjetivoKg = pesoKg;
    }
}
