using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.WinForms.Presentacion.Pesaje;

internal enum LadoPesaje { Izquierdo, Derecho }

internal enum MotivoRechazoCaptura
{
    Ninguno,
    BalanzaNoCapturable,     // !EsCapturable(ahora) (desconectada, inestable, sin peso o lectura vieja)
    PesoFueraDeRango,        // < PesoMinimoLadoKg o > PesoMaximoLadoKg
    EsperandoRetornoACero,
    SimuladorNoPermitido,    // EsSimulador y el usuario no tiene ConfigurarBalanza
    LadoNoPermitido          // Media res con el OTRO lado ya capturado
}

internal sealed class ControlCapturas
{
    private readonly bool _puedeUsarSimulador;

    public ControlCapturas(bool puedeUsarSimulador)
    {
        _puedeUsarSimulador = puedeUsarSimulador;
    }

    public decimal? PesoIzquierdo { get; private set; }
    public decimal? PesoDerecho { get; private set; }
    public bool CapturadoConSimulador { get; private set; }
    public bool EsperandoRetornoACero { get; private set; }
    public bool TienePesos => PesoIzquierdo.HasValue || PesoDerecho.HasValue;

    public void Observar(InstantaneaBalanza instantanea)
    {
        ArgumentNullException.ThrowIfNull(instantanea);

        if (instantanea.Estado == EstadoBalanza.Conectada &&
            instantanea.PesoKg.HasValue &&
            Math.Abs(instantanea.PesoKg.Value) <= ReglasPesaje.UmbralRetornoCeroKg)
        {
            EsperandoRetornoACero = false;
        }
    }

    public MotivoRechazoCaptura Evaluar(LadoPesaje lado, TipoDesposte tipo, InstantaneaBalanza instantanea, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(instantanea);

        if (instantanea.EsSimulador && !_puedeUsarSimulador)
        {
            return MotivoRechazoCaptura.SimuladorNoPermitido;
        }

        if (!instantanea.EsCapturable(ahora))
        {
            return MotivoRechazoCaptura.BalanzaNoCapturable;
        }

        var pesoRedondeado = ReglasPesaje.Redondear(instantanea.PesoKg!.Value);
        if (!ReglasPesaje.EsPesoLadoValido(pesoRedondeado))
        {
            return MotivoRechazoCaptura.PesoFueraDeRango;
        }

        if (EsperandoRetornoACero)
        {
            return MotivoRechazoCaptura.EsperandoRetornoACero;
        }

        if (tipo == TipoDesposte.MediaRes)
        {
            if (lado == LadoPesaje.Izquierdo && PesoDerecho.HasValue)
            {
                return MotivoRechazoCaptura.LadoNoPermitido;
            }
            if (lado == LadoPesaje.Derecho && PesoIzquierdo.HasValue)
            {
                return MotivoRechazoCaptura.LadoNoPermitido;
            }
        }

        return MotivoRechazoCaptura.Ninguno;
    }

    public MotivoRechazoCaptura Capturar(LadoPesaje lado, TipoDesposte tipo, InstantaneaBalanza instantanea, DateTimeOffset ahora)
    {
        var motivo = Evaluar(lado, tipo, instantanea, ahora);
        if (motivo != MotivoRechazoCaptura.Ninguno)
        {
            return motivo;
        }

        var pesoRedondeado = ReglasPesaje.Redondear(instantanea.PesoKg!.Value);
        if (lado == LadoPesaje.Izquierdo)
        {
            PesoIzquierdo = pesoRedondeado;
        }
        else
        {
            PesoDerecho = pesoRedondeado;
        }

        if (instantanea.EsSimulador)
        {
            CapturadoConSimulador = true;
        }

        EsperandoRetornoACero = ReglasPesaje.ExigirRetornoACero;
        return MotivoRechazoCaptura.Ninguno;
    }

    public void BorrarPesos()
    {
        PesoIzquierdo = null;
        PesoDerecho = null;
        CapturadoConSimulador = false;
        EsperandoRetornoACero = false;
    }

    public void ReiniciarTrasGrabar()
    {
        PesoIzquierdo = null;
        PesoDerecho = null;
        CapturadoConSimulador = false;
    }
}
