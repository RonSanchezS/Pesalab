using System.Drawing;
using System.Drawing.Imaging;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

internal static class DatosReporteDePrueba
{
    public static ReportePesajes Crear(
        TipoReporte tipo = TipoReporte.PesajesEntreFechas,
        int filas = 5,
        int simuladas = 0,
        string? direccion = null,
        string? motivo = null,
        string? rutaLogo = null)
    {
        var listaFilas = new List<FilaReporte>();
        var fechaBase = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Unspecified);

        for (int i = 1; i <= filas; i++)
        {
            var tipoDesposte = (i % 3 == 0) ? TipoDesposte.MediaRes : TipoDesposte.ResCompleta;
            decimal izq = tipoDesposte == TipoDesposte.MediaRes ? 106.0m : 109.0m;
            decimal der = tipoDesposte == TipoDesposte.MediaRes ? 0.0m : 106.0m;
            decimal total = izq + der;
            var origen = (i <= simuladas) ? OrigenPeso.Simulador : OrigenPeso.Balanza;
            DateTime? anuladoEn = (tipo == TipoReporte.Anuladas) ? fechaBase.AddHours(2) : null;
            string? anuladoPor = (tipo == TipoReporte.Anuladas) ? "ADMIN" : null;
            string? motivoAnulacion = (tipo == TipoReporte.Anuladas) ? (motivo ?? "ERROR EN DATOS") : null;

            listaFilas.Add(new FilaReporte(
                Item: i,
                PesajeId: i,
                NumeroBoleta: 1000 + i,
                FechaHoraLocal: fechaBase.AddMinutes(i * 15),
                Secuencia: i,
                MarcaGanadero: $"M{(i % 10):D2}",
                NombreGanadero: $"GANADERO {i}",
                CodigoCliente: 100 + (i % 20),
                NombreCliente: $"CLIENTE {i}",
                Direccion: direccion ?? $"AV. PRINCIPAL {i}",
                TipoDesposte: tipoDesposte,
                PesoIzquierdo: izq,
                PesoDerecho: der,
                PesoTotal: total,
                Origen: origen,
                AnuladoEnLocal: anuladoEn,
                AnuladoPor: anuladoPor,
                MotivoAnulacion: motivoAnulacion));
        }

        var totales = CalculadoraTotalesReporte.Calcular(listaFilas);
        var desde = new DateOnly(2026, 8, 1);
        var hasta = tipo == TipoReporte.PesajesDelDia ? desde : new DateOnly(2026, 8, 10);
        string? claveFiltro = tipo == TipoReporte.PorGanadero ? "M01" : tipo == TipoReporte.PorCliente ? "101" : null;
        string titulo = TextosReporte.Titulo(tipo);
        string subtitulo = TextosReporte.Subtitulo(
            tipo,
            desde,
            hasta,
            tipo == TipoReporte.PorGanadero ? "M01" : null,
            tipo == TipoReporte.PorCliente ? "101" : null);

        return new ReportePesajes(
            Tipo: tipo,
            Desde: desde,
            Hasta: hasta,
            ClaveFiltro: claveFiltro,
            Empresa: "MATADERO MUNICIPAL",
            RutaLogo: rutaLogo,
            Titulo: titulo,
            Subtitulo: subtitulo,
            GeneradoPor: "operador1",
            GeneradoEnLocal: new DateTime(2026, 8, 10, 16, 30, 0, DateTimeKind.Unspecified),
            Filas: listaFilas,
            Totales: totales);
    }

    public static string CrearLogo(
        string directorio,
        string extension = ".png",
        int ancho = 400,
        int alto = 120,
        Color? color = null)
    {
        Directory.CreateDirectory(directorio);
        string ruta = Path.Combine(directorio, "logo" + extension);

        using (var bmp = new Bitmap(ancho, alto))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(color ?? Color.FromArgb(255, 0, 0));
            }

            var formato = extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            bmp.Save(ruta, formato);
        }

        return ruta;
    }
}
