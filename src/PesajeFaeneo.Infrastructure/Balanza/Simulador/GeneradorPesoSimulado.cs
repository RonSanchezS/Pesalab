using PesajeFaeneo.Application.Balanza;

namespace PesajeFaeneo.Infrastructure.Balanza.Simulador;

/// <summary>
/// Genera una trayectoria de peso senoidal amortiguada hacia un objetivo (D10), con ruido estocástico.
/// Con estado: cada cambio de objetivo reinicia la amplitud y la fase desde la posición actual.
/// </summary>
internal sealed class GeneradorPesoSimulado
{
    private const double FactorAmortiguacion = 0.3;
    private const double VelocidadAngular = 0.6;
    private const double UmbralAsentamiento = 0.2;

    private bool _inicializado;
    private decimal _objetivoAnterior;
    private double _amplitudInicial;
    private int _paso;

    internal decimal Siguiente(decimal actual, decimal objetivo, Random aleatorio)
    {
        if (!_inicializado || objetivo != _objetivoAnterior)
        {
            _objetivoAnterior = objetivo;
            _amplitudInicial = (double)(actual - objetivo);
            _paso = 0;
            _inicializado = true;
        }
        else
        {
            _paso++;
        }

        double decaimiento = Math.Exp(-FactorAmortiguacion * _paso);
        double envolvente = Math.Abs(_amplitudInicial) * decaimiento;

        decimal siguiente;
        if (envolvente <= UmbralAsentamiento)
        {
            // La envolvente ya decayó lo suficiente: se considera asentada exactamente en el objetivo.
            siguiente = objetivo;
        }
        else
        {
            double oscilacion = _amplitudInicial * decaimiento * Math.Cos(VelocidadAngular * _paso);
            var ruido = (decimal)(aleatorio.NextDouble() - 0.5) * 0.3m;
            siguiente = objetivo + (decimal)oscilacion + ruido;
            siguiente = Math.Round(siguiente, 1, MidpointRounding.AwayFromZero);
        }

        if (siguiente < -10m) siguiente = -10m;
        if (siguiente > ReglasBalanza.PesoMaximoSimuladorKg) siguiente = ReglasBalanza.PesoMaximoSimuladorKg;

        return siguiente;
    }
}
