using System.Globalization;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Reportes;

public static class TextosReporte
{
    public const string EmpresaPorDefecto = "SISTEMA DE PESAJE PARA FAENEO";
    public const string SinBoletas = "No hay boletas para los filtros seleccionados.";
    public const string MaximoSuperado = "El reporte supera el máximo de 20.000 boletas. Reduzca el rango de fechas.";

    public static string NombreTipo(TipoReporte tipo) => tipo switch
    {
        TipoReporte.PesajesDelDia => "Pesajes del día",
        TipoReporte.PesajesEntreFechas => "Pesajes entre fechas",
        TipoReporte.PorGanadero => "Pesajes por ganadero",
        TipoReporte.PorCliente => "Pesajes por cliente",
        TipoReporte.Anuladas => "Boletas anuladas",
        _ => tipo.ToString()
    };

    public static string Titulo(TipoReporte tipo) => tipo switch
    {
        TipoReporte.PesajesDelDia => "Reporte de Pesajes del Día",
        TipoReporte.PesajesEntreFechas => "Reporte de Pesajes Fecha a Fecha",
        TipoReporte.PorGanadero => "Reporte por Ganadero",
        TipoReporte.PorCliente => "Reporte por Cliente Fecha a Fecha",
        TipoReporte.Anuladas => "Reporte de Boletas Anuladas",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de reporte desconocido.")
    };

    public static string Rango(DateOnly desde, DateOnly hasta)
    {
        if (desde == hasta)
        {
            return "Del día: " + desde.ToString("dd/MM/yyyy", TextosBoleta.Cultura);
        }

        return "De fecha: " + desde.ToString("dd/MM/yyyy", TextosBoleta.Cultura) + " a " + hasta.ToString("dd/MM/yyyy", TextosBoleta.Cultura);
    }

    public static string Subtitulo(TipoReporte tipo, DateOnly desde, DateOnly hasta, string? ganadero, string? cliente) => tipo switch
    {
        TipoReporte.PorGanadero => Rango(desde, hasta) + " — Ganadero: " + (ganadero ?? string.Empty),
        TipoReporte.PorCliente => Rango(desde, hasta) + " — Cliente: " + (cliente ?? string.Empty),
        TipoReporte.Anuladas => Rango(desde, hasta) + " (fecha de emisión de la boleta)",
        _ => Rango(desde, hasta)
    };

    public static string Kg(decimal kg) => kg.ToString("#,##0.0", TextosBoleta.Cultura);

    public static string Reses(decimal r) => r.ToString("#,##0.#", TextosBoleta.Cultura);

    public static string Entero(int n) => n.ToString("#,##0", TextosBoleta.Cultura);

    public static string NumeroBoleta(FilaReporte f)
    {
        var numero = f.NumeroBoleta.ToString(CultureInfo.InvariantCulture);
        return f.Origen == OrigenPeso.Simulador ? numero + "*" : numero;
    }

    public static string Fecha(DateTime local) => TextosBoleta.Fecha(local);

    public static string FechaHora(DateTime local) => TextosBoleta.FechaHora(local);

    public static string Origen(OrigenPeso origen) => origen switch
    {
        OrigenPeso.Balanza => "BALANZA",
        OrigenPeso.Simulador => "SIMULADO",
        OrigenPeso.Importado => "IMPORTADA",
        _ => origen.ToString().ToUpperInvariant()
    };

    public static string Generado(string usuario, DateTime local) =>
        "Generado el " + FechaHora(local) + " por " + usuario;

    public static string TotalPesoNeto(TotalesReporte t) =>
        "Total Peso Neto: " + Kg(t.TotalNetoKg) + " Kg.";

    public static string CantidadReses(TotalesReporte t) =>
        "Cantidad Reses: " + Reses(t.Reses);

    public static string DetalleBoletas(TotalesReporte t) =>
        "Boletas: " + Entero(t.Boletas) + " (res completa: " + Entero(t.ResesCompletas) + " · media res: " + Entero(t.MediasReses) + ")";

    public static string BoletasAnuladas(TotalesReporte t) =>
        "Boletas anuladas: " + Entero(t.Boletas);

    public static string PesoNetoAnulado(TotalesReporte t) =>
        "Peso neto anulado: " + Kg(t.TotalNetoKg) + " Kg.";

    public static string? NotaSimuladas(int n) =>
        n == 0 ? null : "(*) " + Entero(n) + " boleta(s) con peso SIMULADO — sin valor comercial.";

    public static string NombreHoja(bool anuladas) => anuladas ? "Anuladas" : "Pesajes";

    public static string NombreArchivo(ReportePesajes r, string extension)
    {
        string prefijo = r.Tipo switch
        {
            TipoReporte.PesajesDelDia => "Pesajes_del_dia",
            TipoReporte.PesajesEntreFechas => "Pesajes",
            TipoReporte.PorGanadero => "Ganadero_" + (r.ClaveFiltro ?? string.Empty),
            TipoReporte.PorCliente => "Cliente_" + (r.ClaveFiltro ?? string.Empty),
            TipoReporte.Anuladas => "Anuladas",
            _ => r.Tipo.ToString()
        };

        string rango = r.Hasta != r.Desde
            ? "_" + r.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_a_" + r.Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "_" + r.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string nombreBase = prefijo + rango;
        char[] invalidos = Path.GetInvalidFileNameChars();
        var saneado = new char[nombreBase.Length];
        for (int i = 0; i < nombreBase.Length; i++)
        {
            char c = nombreBase[i];
            saneado[i] = (invalidos.Contains(c) || char.IsWhiteSpace(c)) ? '_' : c;
        }

        string ext = extension.StartsWith('.') ? extension : "." + extension;
        return new string(saneado) + ext;
    }
}
