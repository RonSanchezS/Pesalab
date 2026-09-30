using System.Globalization;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Boletas.Diseno;

public static class TextosBoleta
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-BO");

    public static string Peso(decimal kg) =>
        kg > 0m ? kg.ToString("0.0", Cultura) + " kg" : "—";

    public static string Sexo(char s) =>
        char.ToUpperInvariant(s) == 'H' ? "HEMBRA" : "MACHO";

    public static string Desposte(TipoDesposte t) => t switch
    {
        TipoDesposte.ResCompleta => "RES COMPLETA",
        TipoDesposte.MediaRes => "MEDIA RES",
        _ => t.ToString().ToUpperInvariant()
    };

    public static string Fecha(DateTime local) => local.ToString("dd/MM/yyyy", Cultura);

    public static string Hora(DateTime local) => local.ToString("HH:mm", Cultura);

    public static string FechaHora(DateTime local) => Fecha(local) + " " + Hora(local);

    public static string PieImpreso(DatosBoleta d) => "Impreso: " + FechaHora(d.GeneradoEnLocal);

    public static string Numero(DatosBoleta d) =>
        d.EsMuestra ? "MUESTRA" : "N° " + d.NumeroBoleta.ToString(Cultura);

    public static string Ganadero(DatosBoleta d) =>
        UnirNoVacios(" - ", d.MarcaGanadero, d.NombreGanadero);

    public static string Cliente(DatosBoleta d) =>
        UnirNoVacios(" - ", d.CodigoCliente.ToString(Cultura), d.NombreCliente);

    public static string Fletero(FleteroBoleta f) =>
        string.IsNullOrWhiteSpace(f.Celular)
            ? "F" + f.Orden.ToString(Cultura) + ": " + f.Nombre
            : "F" + f.Orden.ToString(Cultura) + ": " + f.Nombre + " - " + f.Celular.Trim();

    public static string UnirNoVacios(string separador, params string?[] partes)
    {
        return string.Join(separador, partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
    }

    public static IReadOnlyList<string> Leyendas(DatosBoleta d, int numeroCopia, int totalCopias)
    {
        var lista = new List<string>();

        // Cada leyenda se evalúa por separado (no son excluyentes): una boleta anulada de peso
        // simulado debe seguir diciendo "PESO SIMULADO". Solo la marca de agua tiene prioridad única.
        if (d.EsMuestra)
        {
            lista.Add("MUESTRA — SIN VALOR");
        }

        if (d.Estado == EstadoPesaje.Anulado)
        {
            lista.Add("ANULADA: " + (d.MotivoAnulacion ?? string.Empty));
        }

        if (d.Origen == OrigenPeso.Simulador)
        {
            lista.Add("PESO SIMULADO — SIN VALOR COMERCIAL");
        }

        if (d.Origen == OrigenPeso.Importado)
        {
            lista.Add("IMPORTADA DEL SISTEMA ANTERIOR");
        }

        if (d.EsReimpresion)
        {
            lista.Add("REIMPRESIÓN " + d.ImpresionesPrevias.ToString(Cultura) + " — " + FechaHora(d.GeneradoEnLocal));
        }

        if (totalCopias > 1)
        {
            lista.Add(numeroCopia == 1 ? "ORIGINAL" : "COPIA " + (numeroCopia - 1).ToString(Cultura));
        }

        return lista;
    }

    public static string? MarcaDeAgua(DatosBoleta d)
    {
        if (d.EsMuestra) return "MUESTRA";
        if (d.Estado == EstadoPesaje.Anulado) return "ANULADA";
        if (d.Origen == OrigenPeso.Simulador) return "SIMULADO";
        return null;
    }
}
