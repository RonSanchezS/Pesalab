using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Reportes.Dtos;

public enum TipoReporte
{
    PesajesDelDia = 1,
    PesajesEntreFechas = 2,
    PorGanadero = 3,
    PorCliente = 4,
    Anuladas = 5
}

public sealed record FiltroReporte(
    TipoReporte Tipo,
    DateOnly Desde,
    DateOnly Hasta,               // en PesajesDelDia se ignora (el servicio usa Desde)
    int? GanaderoId,              // solo PorGanadero
    int? ClienteId);              // solo PorCliente

public sealed record OpcionesReportes(
    IReadOnlyList<ElementoBusqueda> Ganaderos,    // incluye inactivos
    IReadOnlyList<ElementoBusqueda> Clientes);    // incluye inactivos

public sealed record FilaReporte(
    int Item,                     // 1..n en el orden del reporte
    int PesajeId,
    long NumeroBoleta,
    DateTime FechaHoraLocal,      // Kind = Unspecified, hora local
    int Secuencia,
    string MarcaGanadero,
    string NombreGanadero,
    int CodigoCliente,
    string NombreCliente,
    string Direccion,             // la de la boleta (no la del cliente)
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    OrigenPeso Origen,
    DateTime? AnuladoEnLocal,     // solo anuladas; Unspecified, hora local
    string? AnuladoPor,           // solo anuladas; NombreCompleto o, si está vacío, NombreUsuario
    string? MotivoAnulacion);     // solo anuladas; completo (sin recortar)

public sealed record TotalesReporte(
    int Boletas,
    int ResesCompletas,
    int MediasReses,
    decimal Reses,
    decimal TotalIzquierdoKg,
    decimal TotalDerechoKg,
    decimal TotalNetoKg,
    int Simuladas);

public sealed record ReportePesajes(
    TipoReporte Tipo,
    DateOnly Desde,
    DateOnly Hasta,               // en PesajesDelDia, igual a Desde
    string? ClaveFiltro,          // marca del ganadero (PorGanadero) o código del cliente (PorCliente); null en los demás
    string Empresa,
    string? RutaLogo,             // ruta completa del logo del sistema, o null si no hay (D10); lo carga el generador
    string Titulo,
    string Subtitulo,
    string GeneradoPor,
    DateTime GeneradoEnLocal,     // Unspecified, hora local
    IReadOnlyList<FilaReporte> Filas,
    TotalesReporte Totales)
{
    public bool EsAnuladas => Tipo == TipoReporte.Anuladas;
}
