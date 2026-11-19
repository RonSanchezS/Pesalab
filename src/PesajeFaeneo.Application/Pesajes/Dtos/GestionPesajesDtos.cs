using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Pesajes.Dtos;

public sealed record FiltroBoletas(
    DateOnly? Desde,
    DateOnly? Hasta,
    long? NumeroBoleta,
    int? GanaderoId,
    int? ClienteId,
    EstadoPesaje? Estado);

public sealed record BoletaEncontrada(
    int Id,
    long NumeroBoleta,
    int Version,
    DateTime FechaHoraLocal,          // Kind = Unspecified, hora local (regla 0.6)
    string Codigo,
    string NombreGanadero,
    int Secuencia,
    string NombreCliente,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    OrigenPeso Origen,
    EstadoPesaje Estado,
    string? MotivoAnulacion,
    string? NombreFletero = null);

public sealed record ResultadoBusquedaBoletas(
    IReadOnlyList<BoletaEncontrada> Boletas,
    bool Truncado,                    // true si había más de BusquedaMaximoFilas
    ModoDesposte ModoDesposte = ModoDesposte.Ambos);   // F11: tipo de pesaje efectivo, para ocultar el desposte (D6)

public sealed record OpcionesBusquedaBoletas(
    IReadOnlyList<ElementoBusqueda> Ganaderos,    // incluye inactivos
    IReadOnlyList<ElementoBusqueda> Clientes);    // incluye inactivos

public sealed record BoletaParaEdicion(
    int Id,
    long NumeroBoleta,
    int Version,
    DateTime FechaHoraLocal,
    int GanaderoId,
    int ClienteId,
    string Direccion,
    int Secuencia,
    string ColorRaza,
    char Sexo,
    string Observacion,
    TipoDesposte TipoDesposte,        // solo lectura
    decimal PesoIzquierdo,            // solo lectura
    decimal PesoDerecho,              // solo lectura
    decimal PesoTotal,                // solo lectura
    OrigenPeso Origen,                // solo lectura
    string NombreOperador,            // solo lectura
    IReadOnlyList<ElementoBusqueda> Ganaderos,    // activos + el actual aunque esté inactivo
    IReadOnlyList<ElementoBusqueda> Clientes,     // ídem
    int? FleteroId = null,
    IReadOnlyList<ElementoBusqueda>? Fleteros = null,    // activos + el actual aunque esté inactivo (F10)
    bool MostrarDesposte = true);                        // F11 (D6): false si coincide con el único tipo que pesa la empresa

public sealed record EditarPesaje(
    int PesajeId,
    int VersionEsperada,
    int GanaderoId,
    int ClienteId,
    string? Direccion,
    int Secuencia,
    string? ColorRaza,
    char Sexo,
    string? Observacion,
    string? Motivo,
    int? FleteroId = null);

public sealed record ResultadoEdicion(
    bool HuboCambios,
    IReadOnlyList<string> CamposModificados);     // etiquetas legibles: "Ganadero", "Secuencia", …

public sealed record AnularPesaje(
    int PesajeId,
    int VersionEsperada,
    string? Motivo);
