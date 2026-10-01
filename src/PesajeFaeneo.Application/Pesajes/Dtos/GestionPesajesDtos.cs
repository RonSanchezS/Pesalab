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
    string Marca,
    string NombreGanadero,
    int Secuencia,
    string NombreCliente,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    OrigenPeso Origen,
    EstadoPesaje Estado,
    string? MotivoAnulacion);

public sealed record ResultadoBusquedaBoletas(
    IReadOnlyList<BoletaEncontrada> Boletas,
    bool Truncado);                   // true si había más de BusquedaMaximoFilas

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
    IReadOnlyList<ElementoBusqueda> Clientes);    // ídem

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
    string? Motivo);

public sealed record ResultadoEdicion(
    bool HuboCambios,
    IReadOnlyList<string> CamposModificados);     // etiquetas legibles: "Ganadero", "Secuencia", …

public sealed record AnularPesaje(
    int PesajeId,
    int VersionEsperada,
    string? Motivo);
