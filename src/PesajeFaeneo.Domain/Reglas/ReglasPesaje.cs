using System.Globalization;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Domain.Reglas;

public static class CamposPesaje
{
    public const string Ganadero = "Ganadero";
    public const string Cliente = "Cliente";
    public const string Direccion = "Direccion";
    public const string Secuencia = "Secuencia";
    public const string ColorRaza = "ColorRaza";
    public const string Sexo = "Sexo";
    public const string TipoDesposte = "TipoDesposte";
    public const string PesoIzquierdo = "PesoIzquierdo";
    public const string PesoDerecho = "PesoDerecho";
    public const string Pesos = "Pesos";
    public const string Observacion = "Observacion";
}

public static class ReglasPesaje
{
    public const decimal PesoMinimoLadoKg = 10.0m;
    public const decimal PesoMaximoLadoKg = 600.0m;
    public const decimal UmbralRetornoCeroKg = 2.0m;
    public const bool ExigirRetornoACero = true;
    public const int SecuenciaMinima = 1;
    public const int SecuenciaMaxima = 9999;
    public const int LongitudMaximaDireccion = 150;
    public const int LongitudMaximaColorRaza = 30;
    public const int LongitudMaximaObservacion = 250;
    public const char SexoMacho = 'M';
    public const char SexoHembra = 'H';
    public const int PesajesDelDiaMaximo = 50;

    public static decimal Redondear(decimal pesoKg) =>
        Math.Round(pesoKg, 1, MidpointRounding.AwayFromZero);

    public static decimal CalcularTotal(decimal izq, decimal der) =>
        Redondear(Redondear(izq) + Redondear(der));

    public static bool EsSexoValido(char sexo)
    {
        var s = char.ToUpperInvariant(sexo);
        return s is SexoMacho or SexoHembra;
    }

    public static bool EsPesoLadoValido(decimal pesoKg)
    {
        var redondeado = Redondear(pesoKg);
        return redondeado >= PesoMinimoLadoKg && redondeado <= PesoMaximoLadoKg;
    }

    public static int SugerirSecuencia(int? maximaDelDia)
    {
        if (maximaDelDia is null)
            return SecuenciaMinima;

        return Math.Min(maximaDelDia.Value + 1, SecuenciaMaxima);
    }

    public static IReadOnlyList<ErrorValidacion> ValidarPesos(TipoDesposte tipo, decimal izq, decimal der)
    {
        var errores = new List<ErrorValidacion>();

        if (!Enum.IsDefined(tipo))
        {
            errores.Add(new ErrorValidacion(CamposPesaje.TipoDesposte, "Seleccione el tipo de desposte."));
            return errores;
        }

        izq = Redondear(izq);
        der = Redondear(der);

        if (izq < 0 || der < 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Pesos, "Los pesos no pueden ser negativos."));
            return errores;
        }

        if (izq == 0 && der == 0)
        {
            errores.Add(new ErrorValidacion(CamposPesaje.Pesos, "Capture al menos un peso."));
            return errores;
        }

        var cultura = CultureInfo.GetCultureInfo("es-BO");
        var rangoMsg = $"El peso de cada lado debe estar entre {PesoMinimoLadoKg.ToString("0.0", cultura)} y {PesoMaximoLadoKg.ToString("0.0", cultura)} kg.";

        if (tipo == TipoDesposte.ResCompleta)
        {
            if (izq == 0)
            {
                errores.Add(new ErrorValidacion(CamposPesaje.PesoIzquierdo, "Res completa: capture el peso izquierdo."));
            }
            else if (!EsPesoLadoValido(izq))
            {
                errores.Add(new ErrorValidacion(CamposPesaje.PesoIzquierdo, rangoMsg));
            }

            if (der == 0)
            {
                errores.Add(new ErrorValidacion(CamposPesaje.PesoDerecho, "Res completa: capture el peso derecho."));
            }
            else if (!EsPesoLadoValido(der))
            {
                errores.Add(new ErrorValidacion(CamposPesaje.PesoDerecho, rangoMsg));
            }
        }
        else if (tipo == TipoDesposte.MediaRes)
        {
            if (izq > 0 && der > 0)
            {
                errores.Add(new ErrorValidacion(CamposPesaje.Pesos, "Media res: capture solo un lado."));
            }
            else if (izq > 0)
            {
                if (!EsPesoLadoValido(izq))
                {
                    errores.Add(new ErrorValidacion(CamposPesaje.PesoIzquierdo, rangoMsg));
                }
            }
            else if (der > 0)
            {
                if (!EsPesoLadoValido(der))
                {
                    errores.Add(new ErrorValidacion(CamposPesaje.PesoDerecho, rangoMsg));
                }
            }
        }

        return errores;
    }
}
