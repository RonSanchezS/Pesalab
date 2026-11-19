using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

public enum TipoImportacion
{
    Ganaderos = 1,
    Clientes = 2,
    Pesajes = 3
}

public enum NivelIncidencia
{
    Error = 1,
    Advertencia = 2,
    Omitida = 3
}

public sealed record Incidencia(int Fila, string Campo, NivelIncidencia Nivel, string Mensaje);

public sealed record CampoImportacion(string Clave, string Titulo, bool Obligatorio, IReadOnlyList<string> Alias);

public sealed record ClienteExistente(int Id, string Direccion);

public sealed record ClavesExistentes(
    IReadOnlyDictionary<string, int> GanaderosPorCodigo,
    IReadOnlyDictionary<int, ClienteExistente> ClientesPorCodigo,
    IReadOnlyDictionary<long, OrigenPeso> BoletasExistentes,
    ModoDesposte ModoDesposte = ModoDesposte.Ambos);   // F11 (D10): tipo de pesaje efectivo, solo para advertir

public sealed record GanaderoAImportar(int Fila, string Codigo, string Nombre, bool Activo);

public sealed record ClienteAImportar(int Fila, int Codigo, string Nombre, string Direccion, bool Activo);

public sealed record PesajeAImportar(
    int Fila, long NumeroBoleta, DateTime FechaHoraUtc, int GanaderoId, int ClienteId, string Direccion,
    int Secuencia, string ColorRaza, char Sexo, TipoDesposte TipoDesposte,
    decimal PesoIzquierdo, decimal PesoDerecho, decimal PesoTotal,
    string Observacion, EstadoPesaje Estado, string? MotivoAnulacion);

public sealed record PlanImportacion(
    TipoImportacion Tipo,
    int FilasLeidas,
    IReadOnlyList<GanaderoAImportar> Ganaderos,
    IReadOnlyList<ClienteAImportar> Clientes,
    IReadOnlyList<PesajeAImportar> Pesajes,
    IReadOnlyList<Incidencia> Incidencias,
    bool IncidenciasTruncadas,
    int FilasConError,
    int FilasOmitidas,
    int FilasConAdvertencia)
{
    public int FilasAImportar => Ganaderos.Count + Clientes.Count + Pesajes.Count;
}

public sealed record VistaOrigen(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<FilaOrigen> Muestra,
    int TotalFilas,
    string Codificacion,
    IReadOnlyList<string> Advertencias,
    IReadOnlyDictionary<string, int?> MapeoSugerido);

/// <summary>Mapeo: clave de campo → índice de columna del archivo (null = sin asignar).</summary>
public sealed record SolicitudImportacion(SolicitudLectura Lectura, TipoImportacion Tipo, IReadOnlyDictionary<string, int?> Mapeo);

public sealed record ResultadoImportacion(
    bool EsSimulacro,
    string Huella,
    TipoImportacion Tipo,
    int FilasLeidas,
    int FilasAImportar,
    int Insertadas,
    int FilasOmitidas,
    int FilasConError,
    int FilasConAdvertencia,
    IReadOnlyList<Incidencia> Incidencias,
    bool IncidenciasTruncadas,
    string? BackupPrevio);
