using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Reportes.Dtos;

public enum TipoReporte
{
    PesajesDelDia = 1,
    PesajesEntreFechas = 2,
    PorGanadero = 3,
    PorCliente = 4,
    Anuladas = 5,
    PorRaza = 6
}

public sealed record FiltroReporte(
    TipoReporte Tipo,
    DateOnly Desde,
    DateOnly Hasta,               // en PesajesDelDia se ignora (el servicio usa Desde)
    int? GanaderoId,              // solo PorGanadero
    int? ClienteId,               // solo PorCliente
    string? ColorRaza = null);    // solo PorRaza (F10, O8): se compara normalizada (sin espacios sobrantes, en mayúsculas)

public sealed record OpcionesReportes(
    IReadOnlyList<ElementoBusqueda> Ganaderos,    // incluye inactivos
    IReadOnlyList<ElementoBusqueda> Clientes,     // incluye inactivos
    IReadOnlyList<string>? Razas = null);         // valores distintos de Color/Raza (vigentes y anuladas), normalizados y sin vacíos

public sealed record FilaReporte(
    int Item,                     // 1..n en el orden del reporte
    int PesajeId,
    long NumeroBoleta,
    DateTime FechaHoraLocal,      // Kind = Unspecified, hora local
    int Secuencia,
    string CodigoGanadero,
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
    string? MotivoAnulacion,      // solo anuladas; completo (sin recortar)
    string NombreFletero = "");   // F10 (O5): vacío si el pesaje no tiene fletero

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
    string? ClaveFiltro,          // código del ganadero (PorGanadero), código del cliente (PorCliente) o raza (PorRaza); null en los demás
    string Empresa,
    string? RutaLogo,             // ruta completa del logo del sistema, o null si no hay (D10); lo carga el generador
    string Titulo,
    string Subtitulo,
    string GeneradoPor,
    DateTime GeneradoEnLocal,     // Unspecified, hora local
    IReadOnlyList<FilaReporte> Filas,
    TotalesReporte Totales,
    bool MostrarDesposte = true)  // F11 (D6, D9): false si la empresa pesa un solo tipo y todas las filas son de ese tipo
{
    public bool EsAnuladas => Tipo == TipoReporte.Anuladas;
}
