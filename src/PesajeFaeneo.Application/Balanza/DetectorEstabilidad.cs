namespace PesajeFaeneo.Application.Balanza;

/// <summary>
/// Detector de estabilidad basado en ventana deslizante y reporte de trama (D5).
/// No es seguro para uso concurrente; lo usa exclusivamente el bucle de lectura.
/// </summary>
public sealed class DetectorEstabilidad
{
    private readonly int _lecturasParaEstable;
    private readonly decimal _toleranciaKg;
    private readonly Queue<decimal> _ventana;

    public bool Estable { get; private set; }

    public DetectorEstabilidad(int lecturasParaEstable, decimal toleranciaKg)
    {
        if (lecturasParaEstable < ReglasBalanza.LecturasMinimas || lecturasParaEstable > ReglasBalanza.LecturasMaximas)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lecturasParaEstable),
                lecturasParaEstable,
                $"Las lecturas para estabilidad deben estar entre {ReglasBalanza.LecturasMinimas} y {ReglasBalanza.LecturasMaximas}.");
        }

        if (toleranciaKg < ReglasBalanza.ToleranciaMinimaKg || toleranciaKg > ReglasBalanza.ToleranciaMaximaKg)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toleranciaKg),
                toleranciaKg,
                $"La tolerancia debe estar entre {ReglasBalanza.ToleranciaMinimaKg} y {ReglasBalanza.ToleranciaMaximaKg} kg.");
        }

        _lecturasParaEstable = lecturasParaEstable;
        _toleranciaKg = toleranciaKg;
        _ventana = new Queue<decimal>(lecturasParaEstable);
    }

    public bool Agregar(decimal pesoKg, bool? estableReportado = null)
    {
        if (estableReportado == false)
        {
            Reiniciar();
            return false;
        }

        _ventana.Enqueue(pesoKg);
        while (_ventana.Count > _lecturasParaEstable)
        {
            _ventana.Dequeue();
        }

        if (_ventana.Count < _lecturasParaEstable)
        {
            Estable = false;
            return false;
        }

        decimal min = decimal.MaxValue;
        decimal max = decimal.MinValue;
        foreach (var p in _ventana)
        {
            if (p < min) min = p;
            if (p > max) max = p;
        }

        Estable = (max - min) <= _toleranciaKg;
        return Estable;
    }

    public void Reiniciar()
    {
        _ventana.Clear();
        Estable = false;
    }
}
