using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Pesajes.Dtos;

public sealed record RegistrarPesaje(
    int GanaderoId,
    int ClienteId,
    string? Direccion,
    int Secuencia,
    string? ColorRaza,
    char Sexo,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    string? Observacion,
    bool CapturadoConSimulador,
    int? FleteroId = null);

public sealed record PesajeRegistrado(
    int Id,
    long NumeroBoleta,
    DateTime FechaHoraUtc,
    decimal PesoTotal,
    OrigenPeso Origen);

public sealed record SugerenciaSecuencia(
    int Siguiente,
    int PesajesVigentesHoy);

public sealed record PesajeResumen(
    int Id,
    long NumeroBoleta,
    DateTime FechaHoraUtc,
    string Codigo,
    string NombreGanadero,
    int Secuencia,
    string NombreCliente,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    EstadoPesaje Estado,
    OrigenPeso Origen,
    string? NombreFletero = null);
